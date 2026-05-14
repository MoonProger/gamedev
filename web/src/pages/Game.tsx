import React, { useEffect, useState, useCallback, useRef } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { Unity, useUnityContext } from "react-unity-webgl";
import { api } from '../services/api';
import Button from '../components/ui/Button';
import Loader from '../components/ui/Loader';
import { useToast } from '../context/ToastContext';
import { WsClientToServer, WsServerToClient } from '../types/ws-protocol';
import './Game.css';

interface Player {
  userId: string;
  username: string;
  isReady: boolean;
}

type DevDeltaState = {
  money: number;
  experience: number;
  success: number;
  volounteer: number;
  science: number;
  art: number;
  media: number;
  business: number;
  sport: number;
  tourism: number;
  it: number;
  grants: number;
};
type DevAdjustMode = 'delta' | 'set';

const INITIAL_DEV_DELTAS: DevDeltaState = {
  money: 0,
  experience: 0,
  success: 0,
  volounteer: 0,
  science: 0,
  art: 0,
  media: 0,
  business: 0,
  sport: 0,
  tourism: 0,
  it: 0,
  grants: 0,
};

const SERVER_ERROR_RU: Record<string, string> = {
  ROOM_NOT_FOUND: 'Комната не найдена',
  GAME_ALREADY_STARTED: 'Игра уже началась',
  NOT_ALL_READY_OR_TOO_FEW_PLAYERS: 'Не все игроки готовы или игроков недостаточно для старта',
  GAME_NOT_STARTED: 'Игра еще не началась',
  GAME_PAUSED: 'Игра на паузе',
  INVALID_CARD_CLOSE_EVENT: 'Некорректное закрытие карточки',
  NO_PENDING_GREEN_CHOICE: 'Сейчас нет ожидаемого выбора по зеленой карте',
  NOT_YOUR_GREEN_CHOICE: 'Это не ваш выбор по зеленой карте',
  GREEN_CHOICE_CARD_MISMATCH: 'Выбор сделан не для той зеленой карты',
  GAME_FINISH_PENDING_CARD_CLOSE: 'Сначала закройте победную карту',
  GREEN_CHOICE_REQUIRED: 'Сначала выберите цель по зеленой карте',
  CARD_CLOSE_REQUIRED: 'Сначала закройте открытую карту',
  CHARACTER_SELECTION_PENDING: 'Дождитесь окончания выбора персонажей',
  TARGET_PLAYER_NOT_IN_ROOM: 'Выбранный игрок не находится в комнате',
  NOT_YOUR_TURN: 'Сейчас не ваш ход',
  WRONG_PHASE: 'Сейчас это действие недоступно',
  ROLL_DICE_FIRST: 'Сначала бросьте кубик',
  INVALID_STEPS: 'Некорректное количество шагов',
  STEPS_MUST_EQUAL_DICE: 'Количество шагов должно совпадать с броском кубика',
  MOVE_TARGET_REQUIRED: 'Не выбрана целевая клетка для хода',
  INVALID_MOVE_PATH: 'Нельзя перейти на эту клетку по правилам доски',
  WRONG_PHASE_FOR_CARD: 'Сейчас нельзя тянуть карту',
  CARD_DECK_NOT_FOUND_FOR_SECTOR: 'Для этой клетки нет соответствующей колоды',
  GRANT_REQUIRES_LEVEL_10_SPHERE: 'Для заявки на грант нужен 10-й уровень хотя бы в одной сфере',
  TRAVEL_NOT_ENOUGH_MONEY: 'Недостаточно денег для путешествия',
  CARD_DECK_EMPTY: 'Колода пуста',
  WRONG_PHASE_FOR_PROJECT: 'Сейчас нельзя выполнять проект',
  PROJECT_ACTION_NOT_ALLOWED_ON_THIS_SECTOR: 'На этой клетке нельзя выполнять проект',
  NO_AVAILABLE_PROJECTS: 'Нет доступных проектов',
  NOT_ENOUGH_RESOURCES_FOR_PROJECT: 'Недостаточно ресурсов для проекта',
};

const SAD_SOUND_ERROR_CODES = new Set([
  'GRANT_REQUIRES_LEVEL_10_SPHERE',
  'NOT_ENOUGH_RESOURCES_FOR_PROJECT',
  'NO_AVAILABLE_PROJECTS',
  'TRAVEL_NOT_ENOUGH_MONEY',
]);

const SECTOR_NAME_BY_ID: Record<number, string> = {
  0: 'Айти',
  1: 'Бизнес',
  2: 'Волонтерство',
  3: 'Грант',
  4: 'Деньги',
  5: 'Медиа',
  6: 'Наука',
  7: 'Путешествие',
  8: 'Спорт',
  9: 'Твой проект',
  10: 'Творчество',
  11: 'Туризм',
};

const DECK_NAME_RU: Record<string, string> = {
  science: 'Наука',
  business: 'Бизнес',
  volounteer: 'Волонтерство',
  art: 'Творчество',
  media: 'Медиа',
  sport: 'Спорт',
  tourism: 'Туризм',
  it: 'Айти',
  travel: 'Путешествие',
  grant_success: 'Грант',
};

const PROJECT_NAME_RU: Record<string, string> = {
  science: 'Наука',
  business: 'Бизнес',
  volounteer: 'Волонтерство',
  art: 'Творчество',
  media: 'Медиа',
  sport: 'Спорт',
  tourism: 'Туризм',
  it: 'Айти',
};

const STAT_NAME_RU: Record<string, string> = {
  money: 'деньги',
  experience: 'опыт',
  success: 'успех',
  volounteer: 'волонтерство',
  science: 'наука',
  art: 'творчество',
  media: 'медиа',
  business: 'бизнес',
  sport: 'спорт',
  tourism: 'туризм',
  it: 'айти',
  grants: 'гранты',
};

type PlayerSnapshot = {
  money: number;
  experience: number;
  success: number;
  volounteer: number;
  science: number;
  art: number;
  media: number;
  business: number;
  sport: number;
  tourism: number;
  it: number;
  grants: number;
};

function translateServerError(message: string): string {
  const key = String(message ?? '');
  return SERVER_ERROR_RU[key] ?? key;
}

function formatSigned(n: number): string {
  return `${n > 0 ? '+' : ''}${n}`;
}

function formatSectorName(id: unknown): string {
  const n = Number(id);
  if (!Number.isInteger(n)) return 'неизвестная ячейка';
  return SECTOR_NAME_BY_ID[n] ?? `ячейка ${n}`;
}

function formatDeckName(deckKey: unknown): string {
  const key = String(deckKey ?? '').toLowerCase();
  return DECK_NAME_RU[key] ?? (key ? key : 'неизвестная колода');
}

function formatProjectName(projectId: unknown): string {
  const key = String(projectId ?? '').toLowerCase();
  return PROJECT_NAME_RU[key] ?? (key ? key : 'неизвестный проект');
}

function formatDeltaList(deltas: any[]): string {
  if (!Array.isArray(deltas) || deltas.length === 0) return 'без изменения характеристик';
  const chunks = deltas
    .map((d) => {
      const stat = String(d?.stat ?? '');
      const delta = Number(d?.delta ?? 0);
      if (!stat || !Number.isFinite(delta) || delta === 0) return '';
      return `${formatSigned(delta)} ${STAT_NAME_RU[stat] ?? stat}`;
    })
    .filter(Boolean);
  return chunks.length ? chunks.join(', ') : 'без изменения характеристик';
}

function formatCardChecks(checks: any): string {
  if (!checks || typeof checks !== 'object') return '';
  const parts: string[] = [];
  if (checks.blueDiceSum != null) {
    const exp = Number(checks.blueExperience ?? 0);
    const dice = Number(checks.blueDiceSum ?? 0);
    const ok = Boolean(checks.blueSuccess);
    parts.push(`Синяя проверка: опыт ${exp} против суммы кубиков ${dice} (${ok ? 'успех' : 'неудача'}).`);
  }
  if (checks.redRequiredLevel != null) {
    const level = Number(checks.redSphereLevel ?? 0);
    const need = Number(checks.redRequiredLevel ?? 5);
    const ok = Boolean(checks.redSuccess);
    parts.push(`Красная проверка: уровень сферы ${level} из ${need} (${ok ? 'условие выполнено' : 'условие не выполнено'}).`);
  }
  if (checks.greenMode) {
    const mode = String(checks.greenMode);
    if (mode === 'coop') {
      parts.push('Зеленая карта: выбран кооперативный эффект (участники делят эффект).');
    } else if (mode === 'solo') {
      parts.push('Зеленая карта: кооперация не выбрана, применен одиночный эффект.');
    } else if (mode === 'pending') {
      parts.push('Зеленая карта: ожидается выбор цели для применения эффекта.');
    }
  }
  if (checks.grantDiceSum != null) {
    const dice = Number(checks.grantDiceSum ?? 0);
    const ok = Boolean(checks.grantSuccess);
    parts.push(`Проверка гранта: сумма кубиков ${dice} (${ok ? 'успех, грант получен' : 'неудача, грант не получен'}).`);
  }
  return parts.join(' ');
}

function normalizeRoomPlayers(rawRoom: any): Player[] {
  if (!Array.isArray(rawRoom?.players)) return [];
  return rawRoom.players.map((p: any) => ({
    userId: p?.userId ?? '',
    username:
      typeof p?.username === 'string'
        ? p.username
        : typeof p?.user?.username === 'string'
        ? p.user.username
        : '',
    isReady: Boolean(p?.isReady),
  }));
}

type UnityPlayerState = {
  userId: string;
  position: number;
  grants: number;
  selectedCharacterId?: string;
  completedProjects: string[];
  playerState: {
    money: number;
    experience: number;
    success: number;
    volounteer: number;
    science: number;
    art: number;
    media: number;
    business: number;
    sport: number;
    tourism: number;
    it: number;
  };
};

type UnityGameState = {
  started: boolean;
  isPaused: boolean;
  activePlayerId: string;
  phase: string;
  hasLastDice: boolean;
  lastDice: number;
  currentTurnNumber: number;
  players: UnityPlayerState[];
};

type TurnTimerState = {
  running: boolean;
  durationMs: number;
  remainingMs: number;
  endsAt: number;
  activePlayerId: string | null;
};

type FrontLogEntry = {
  id: string;
  text: string;
  kind: 'info' | 'success' | 'warn' | 'error';
  at: string;
  turn: number;
  actorColor?: 'red' | 'blue' | 'green' | 'yellow';
  actorTitle?: string;
};

type ParsedHistoryEntry = {
  text: string;
  actorId?: string;
  kind: FrontLogEntry['kind'];
  closesTurn: boolean;
};

function formatTimerSeconds(ms: number): string {
  const clamped = Math.max(0, Math.ceil(ms / 1000));
  const minutes = Math.floor(clamped / 60);
  const seconds = clamped % 60;
  return `${minutes}:${String(seconds).padStart(2, '0')}`;
}

function formatHistoryEntry(entry: any): ParsedHistoryEntry | null {
  const type = String(entry?.type ?? '');
  const player = String(entry?.playerId ?? '');
  const common = { actorId: player || undefined, kind: 'info' as const };
  switch (type) {
    case 'character_select':
      return { ...common, text: 'выбрал персонажа.', closesTurn: false };
    case 'character_select_auto':
      return { ...common, text: 'получил персонажа автоматически по таймеру.', closesTurn: false };
    case 'roll_dice':
      return { ...common, text: `бросил кубик: ${Number(entry?.dice ?? 0)}.`, closesTurn: false };
    case 'move':
      return {
        ...common,
        text: `переместился: ${formatSectorName(entry?.fromSector)} -> ${formatSectorName(entry?.toSector)}.`,
        closesTurn: false,
      };
    case 'travel_paid':
      return {
        ...common,
        text: `оплатил путешествие: ${formatSigned(Number(entry?.amount ?? 0))} деньги.`,
        closesTurn: false,
      };
    case 'card':
      {
        const checksText = formatCardChecks(entry?.checks);
        const chained = Array.isArray(entry?.chainedCards) ? entry.chainedCards : [];
        const chainedText =
          chained.length > 0
            ? ` Дополнительно сработали связанные карточки: ${chained
                .map((c: any) => `${formatDeckName(c?.deckKey)} (${formatDeltaList(c?.deltas ?? [])})`)
                .join('; ')}.`
            : '';
      return {
        ...common,
        text: `сыграл карту из колоды "${formatDeckName(entry?.deckKey)}": ${formatDeltaList(entry?.deltas)}. ${
          checksText || 'Проверок не было, применены базовые эффекты карты.'
        }${chainedText}`,
        closesTurn: true,
      };
      }
    case 'card_green_pending':
      return {
        ...common,
        text: `вытянул зеленую карту из колоды "${formatDeckName(entry?.deckKey)}": нужно выбрать цель эффекта.`,
        closesTurn: false,
      };
    case 'project':
      return {
        ...common,
        text: `завершил проект "${formatProjectName(entry?.projectId)}" (${entry?.payment === 'grant' ? 'оплата грантом' : 'оплата деньгами'}), награда: ${formatSigned(Number(entry?.successPoints ?? 0))} успех.`,
        closesTurn: true,
      };
    case 'grant_unavailable':
    case 'project_unavailable':
    case 'project_no_resources':
    case 'travel_unavailable':
      return {
        ...common,
        text: `действие недоступно: ${translateServerError(String(entry?.reason ?? type))}.`,
        closesTurn: true,
        kind: 'warn',
      };
    case 'money_reward':
      return {
        ...common,
        text: `получил награду за ячейку "${formatSectorName(entry?.sector)}": ${formatSigned(Number(entry?.amount ?? 0))} деньги.`,
        closesTurn: false,
      };
    case 'empty_action':
      return {
        ...common,
        text: `завершил ход без дополнительного действия на ячейке "${formatSectorName(entry?.sector)}".`,
        closesTurn: true,
      };
    case 'dev_adjust_stats':
      return {
        actorId: String(entry?.by ?? '') || undefined,
        text: `изменил параметры игрока: ${formatDeltaList(
          Object.entries(entry?.deltas ?? {}).map(([stat, delta]) => ({ stat, delta }))
        )}.`,
        kind: 'warn',
        closesTurn: false,
      };
    default:
      return null;
  }
}

function getPlayerColorInfo(userId: string | undefined, players: Player[]) {
  if (!userId) return null;
  const idx = players.findIndex((p) => p.userId === userId);
  if (idx < 0) return null;
  if (idx === 0) return { key: 'red' as const, title: 'Красный' };
  if (idx === 1) return { key: 'blue' as const, title: 'Синий' };
  if (idx === 2) return { key: 'green' as const, title: 'Зеленый' };
  if (idx === 3) return { key: 'yellow' as const, title: 'Желтый' };
  return null;
}

function buildSnapshotByUser(raw: any): Record<string, PlayerSnapshot> {
  const scores = raw?.scores ?? {};
  const money = raw?.money ?? {};
  const experience = raw?.experience ?? {};
  const deckState = raw?.deckState ?? {};
  const ids = Array.from(
    new Set([
      ...Object.keys(scores),
      ...Object.keys(money),
      ...Object.keys(experience),
      ...Object.keys(deckState),
    ])
  );
  const snapshot: Record<string, PlayerSnapshot> = {};
  for (const userId of ids) {
    const s = scores?.[userId] ?? {};
    snapshot[userId] = {
      money: Number(money?.[userId] ?? 0),
      experience: Number(experience?.[userId] ?? 0),
      success: Number(s.success ?? 0),
      volounteer: Number(s.volounteer ?? 0),
      science: Number(s.science ?? 0),
      art: Number(s.art ?? 0),
      media: Number(s.media ?? 0),
      business: Number(s.business ?? 0),
      sport: Number(s.sport ?? 0),
      tourism: Number(s.tourism ?? 0),
      it: Number(s.it ?? 0),
      grants: Number(deckState?.[userId]?.grants ?? 0),
    };
  }
  return snapshot;
}

function toUnityGameStatePayload(raw: any, knownPlayers: Player[]): UnityGameState {
  const knownUserIds = knownPlayers.map((p) => p.userId);
  const idsFromPositions = Object.keys(raw?.positions ?? {});
  const idsFromScores = Object.keys(raw?.scores ?? {});
  const allIds = Array.from(new Set([...knownUserIds, ...idsFromPositions, ...idsFromScores]));

  const players: UnityPlayerState[] = allIds.map((userId) => {
    const scores = raw?.scores?.[userId] ?? {};
    const deckState = raw?.deckState?.[userId] ?? {};
    const completedProjectsMap = deckState.completedProjects ?? {};
    const completedProjects = Object.keys(completedProjectsMap).filter((k) => completedProjectsMap[k]);
    return {
      userId,
      position: Number(raw?.positions?.[userId] ?? 0),
      grants: Number(deckState.grants ?? 0),
      selectedCharacterId:
        typeof deckState?.selectedCharacter?.characterId === 'string'
          ? deckState.selectedCharacter.characterId
          : undefined,
      completedProjects,
      playerState: {
        money: Number(raw?.money?.[userId] ?? 0),
        experience: Number(raw?.experience?.[userId] ?? 0),
        success: Number(scores.success ?? 0),
        volounteer: Number(scores.volounteer ?? 0),
        science: Number(scores.science ?? 0),
        art: Number(scores.art ?? 0),
        media: Number(scores.media ?? 0),
        business: Number(scores.business ?? 0),
        sport: Number(scores.sport ?? 0),
        tourism: Number(scores.tourism ?? 0),
        it: Number(scores.it ?? 0),
      },
    };
  });

  const lastDiceRaw = raw?.lastDice;
  const hasLastDice = typeof lastDiceRaw === 'number' && Number.isFinite(lastDiceRaw);
  const historyItems = Array.isArray(raw?.history) ? raw.history : [];
  const finishedTurns = historyItems.filter((entry: any) => {
    const type = String(entry?.type ?? '');
    return (
      type === 'card' ||
      type === 'project' ||
      type === 'grant_unavailable' ||
      type === 'money_reward' ||
      type === 'empty_action' ||
      type === 'project_unavailable' ||
      type === 'project_no_resources'
    );
  }).length;
  return {
    started: Boolean(raw?.started),
    isPaused: Boolean(raw?.isPaused),
    activePlayerId: typeof raw?.activePlayerId === 'string' ? raw.activePlayerId : '',
    phase: typeof raw?.phase === 'string' ? raw.phase : 'WAITING_ROLL',
    hasLastDice,
    lastDice: hasLastDice ? Number(lastDiceRaw) : 0,
    currentTurnNumber: Math.max(1, finishedTurns + 1),
    players,
  };
}

const Game: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const [players, setPlayers] = useState<Player[]>([]);
  const [loading, setLoading] = useState(true);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [gameState, setGameState] = useState<any>(null);
  const [localUserId, setLocalUserId] = useState<string | null>(null);
  const [turnTimer, setTurnTimer] = useState<TurnTimerState | null>(null);
  const [timerNowMs, setTimerNowMs] = useState(() => Date.now());
  const [frontLog, setFrontLog] = useState<FrontLogEntry[]>([]);
  const [isLogOpen, setIsLogOpen] = useState(false);
  const [logPos, setLogPos] = useState({ x: 20, y: 170 });
  const [logPanelSize, setLogPanelSize] = useState({ width: 520, height: 340 });
  const [showDevPanel, setShowDevPanel] = useState(false);
  const [devTargetUserId, setDevTargetUserId] = useState<string>('');
  const [devDeltas, setDevDeltas] = useState<DevDeltaState>(INITIAL_DEV_DELTAS);
  const [devAdjustMode, setDevAdjustMode] = useState<DevAdjustMode>('delta');
  const isPausedRef = useRef(false);
  const playersRef = useRef<Player[]>([]);
  const isLoadedRef = useRef(false);
  const unityInitKeyRef = useRef<string | null>(null);
  const unityBootstrapTimeoutsRef = useRef<number[]>([]);
  const fullscreenRootRef = useRef<HTMLDivElement>(null);
  const gameContainerRef = useRef<HTMLDivElement>(null);
  const wsRef = useRef<WebSocket | null>(null);
  const historyCursorRef = useRef(0);
  const historyTurnRef = useRef(1);
  const prevSnapshotRef = useRef<Record<string, PlayerSnapshot> | null>(null);
  const prevActivePlayerRef = useRef<string | null>(null);
  const draggingLogRef = useRef(false);
  const resizingLogRef = useRef(false);
  const dragStartRef = useRef({ x: 0, y: 0 });
  const resizeStartRef = useRef({ x: 0, y: 0, width: 520, height: 340 });
  const { showToast } = useToast();

  useEffect(() => {
    return () => {
      unityInitKeyRef.current = null;
      unityBootstrapTimeoutsRef.current.forEach((id) => window.clearTimeout(id));
      unityBootstrapTimeoutsRef.current = [];
    };
  }, []);
  const roomPassword =
    (location.state as { roomPassword?: string } | null)?.roomPassword ??
    (id ? localStorage.getItem(`room_${id}_password`) : null);

  const { 
    unityProvider, 
    isLoaded, 
    loadingProgression, 
    sendMessage,
    addEventListener,
    removeEventListener,
    unload
  } = useUnityContext({
    loaderUrl: "/Build/Build.loader.js",
    dataUrl: "/Build/Build.data.gz",
    frameworkUrl: "/Build/Build.framework.js.gz",
    codeUrl: "/Build/Build.wasm.gz",
  });

  useEffect(() => {
    setFrontLog([]);
    historyCursorRef.current = 0;
    historyTurnRef.current = 1;
    prevSnapshotRef.current = null;
    prevActivePlayerRef.current = null;
    setTurnTimer(null);
    setTimerNowMs(Date.now());
    setIsLogOpen(false);
    setLogPos({ x: 20, y: 170 });
    setLogPanelSize({ width: 520, height: 340 });
  }, [id]);

  const appendFrontLogEntries = useCallback((entries: Array<Omit<FrontLogEntry, 'id' | 'at'>>) => {
    const normalized = entries.filter((entry) => entry.text && entry.text.trim().length > 0);
    if (!normalized.length) return;
    setFrontLog((prev) => {
      const now = Date.now();
      const nextItems = normalized.map((entry, index) => ({
        id: `${now}-${index}-${Math.random().toString(36).slice(2, 7)}`,
        text: entry.text,
        kind: entry.kind,
        turn: Math.max(1, entry.turn),
        actorColor: entry.actorColor,
        actorTitle: entry.actorTitle,
        at: new Date().toISOString(),
      }));
      const next = [...prev, ...nextItems];
      return next.length > 250 ? next.slice(next.length - 250) : next;
    });
  }, []);

  const pushFrontLog = useCallback((entry: Omit<FrontLogEntry, 'id' | 'at'>) => {
    appendFrontLogEntries([entry]);
  }, [appendFrontLogEntries]);

  useEffect(() => {
    if (id) {
      loadRoomData();
    }
  }, [id]);

  useEffect(() => {
    playersRef.current = players;
  }, [players]);

  useEffect(() => {
    isLoadedRef.current = isLoaded;
  }, [isLoaded]);

  useEffect(() => {
    if (!players.length) return;
    if (devTargetUserId && players.some((p) => p.userId === devTargetUserId)) return;
    const preferred = localUserId && players.some((p) => p.userId === localUserId) ? localUserId : players[0].userId;
    setDevTargetUserId(preferred);
  }, [players, localUserId, devTargetUserId]);

  useEffect(() => {
    if (!turnTimer?.running) return;
    const id = window.setInterval(() => {
      setTimerNowMs(Date.now());
    }, 100);
    return () => window.clearInterval(id);
  }, [turnTimer?.running, turnTimer?.endsAt]);

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (draggingLogRef.current) {
        const dx = e.clientX - dragStartRef.current.x;
        const dy = e.clientY - dragStartRef.current.y;
        setLogPos((prev) => ({
          x: Math.max(8, prev.x + dx),
          y: Math.max(8, prev.y + dy),
        }));
        dragStartRef.current = { x: e.clientX, y: e.clientY };
      }
      if (resizingLogRef.current) {
        const dx = e.clientX - resizeStartRef.current.x;
        const dy = e.clientY - resizeStartRef.current.y;
        setLogPanelSize({
          width: Math.min(window.innerWidth - 12, Math.max(340, resizeStartRef.current.width + dx)),
          height: Math.min(window.innerHeight - 12, Math.max(220, resizeStartRef.current.height + dy)),
        });
      }
    };
    const onUp = () => {
      draggingLogRef.current = false;
      resizingLogRef.current = false;
    };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => {
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
  }, []);

  const loadRoomData = async () => {
    try {
      const data = await api.getRoom(id!);
      const typedPlayers = normalizeRoomPlayers(data.room);
      setPlayers(typedPlayers);
    } catch (err) {
      console.error('Ошибка загрузки комнаты:', err);
      showToast('Не удалось загрузить данные комнаты', 'error');
    } finally {
      setLoading(false);
    }
  };

  const sendWsMessage = useCallback((message: WsClientToServer) => {
    if (wsRef.current?.readyState !== WebSocket.OPEN) {
      showToast('Нет подключения к серверу', 'error');
      return;
    }
    wsRef.current.send(JSON.stringify(message));
  }, [showToast]);

  const getPlayerLabel = useCallback((userId: string) => {
    const player = players.find((p) => p.userId === userId);
    if (!player) return userId;
    const safe = player.username && player.username.trim().length > 0 ? player.username : userId;
    return safe;
  }, [players]);

  const applyDevAdjust = useCallback(() => {
    if (!devTargetUserId) {
      showToast('Выберите игрока для изменения статов', 'error');
      return;
    }
    const hasChanges = Object.values(devDeltas).some((v) => Number(v) !== 0);
    if (devAdjustMode === 'delta' && !hasChanges) {
      showToast('Укажите хотя бы одно изменение стата', 'error');
      return;
    }

    sendWsMessage({
      type: 'game.dev_adjust_stats',
      payload: {
        targetUserId: devTargetUserId,
        mode: devAdjustMode,
        deltas: { ...devDeltas },
      },
    });
    showToast(
      `${devAdjustMode === 'set' ? 'Установлены значения' : 'Применены изменения'} для ${getPlayerLabel(devTargetUserId)}`,
      'success'
    );
    setDevDeltas({ ...INITIAL_DEV_DELTAS });
  }, [devTargetUserId, devDeltas, devAdjustMode, getPlayerLabel, sendWsMessage, showToast]);

  // Подключение к WebSocket для получения игровых событий
  useEffect(() => {
    if (!id) return;

    const token = api.getToken();
    if (!token) {
      console.error('No token');
      return;
    }

    const ws = new WebSocket(`ws://localhost:4000/ws?token=${token}`);
    wsRef.current = ws;

    ws.onopen = () => {
      console.log('Game WebSocket connected');
      ws.send(JSON.stringify({
        type: 'room.join',
        payload: { roomId: id, password: roomPassword || undefined }
      }));
    };

    ws.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data) as WsServerToClient;

        switch (data.type) {
          case 'connected':
            setLocalUserId(data.payload.userId);
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'SetLocalUserId', data.payload.userId);
            }
            break;
          case 'game.state':
            {
              const rawState = data.payload as any;
              const historyItems = Array.isArray(rawState?.history) ? rawState.history : [];
              const batch: Array<Omit<FrontLogEntry, 'id' | 'at'>> = [];
              if (historyItems.length < historyCursorRef.current) {
                historyCursorRef.current = 0;
                historyTurnRef.current = 1;
              }
              if (historyItems.length > historyCursorRef.current) {
                let nextTurn = historyTurnRef.current;
                for (let i = historyCursorRef.current; i < historyItems.length; i++) {
                  const rawEntry = historyItems[i];
                  const parsed = formatHistoryEntry(rawEntry);
                  if (parsed) {
                    const color = getPlayerColorInfo(parsed.actorId, playersRef.current);
                    batch.push({
                      text: parsed.text,
                      kind: parsed.kind,
                      turn: nextTurn,
                      actorColor: color?.key,
                      actorTitle: color?.title,
                    });
                    if (parsed.closesTurn) nextTurn += 1;
                  }
                  if (
                    String(rawEntry?.type ?? '') === 'card' &&
                    String(rawEntry?.checks?.greenMode ?? '') === 'coop' &&
                    typeof rawEntry?.partnerUserId === 'string'
                  ) {
                    const partnerColor = getPlayerColorInfo(rawEntry.partnerUserId, playersRef.current);
                    batch.push({
                      text: `По зеленой карте партнер: ${partnerColor?.title ?? 'Игрок'}. Получено: ${formatDeltaList(
                        Array.isArray(rawEntry?.partnerDeltas) ? rawEntry.partnerDeltas : []
                      )}.`,
                      kind: 'info',
                      turn: nextTurn > 1 ? nextTurn - 1 : nextTurn,
                      actorColor: partnerColor?.key,
                      actorTitle: partnerColor?.title,
                    });
                  }
                }
                historyCursorRef.current = historyItems.length;
                historyTurnRef.current = nextTurn;
              }

              const currentSnapshot = buildSnapshotByUser(rawState);
              const prevSnapshot = prevSnapshotRef.current;
              const prevActivePlayerId = prevActivePlayerRef.current;
              const currentActivePlayerId =
                typeof rawState?.activePlayerId === 'string' ? rawState.activePlayerId : null;

              if (prevSnapshot && prevActivePlayerId && currentActivePlayerId && prevActivePlayerId !== currentActivePlayerId) {
                const summaryByPlayer: string[] = [];
                const allUserIds = Array.from(
                  new Set([...Object.keys(prevSnapshot), ...Object.keys(currentSnapshot)])
                );
                for (const userId of allUserIds) {
                  const before = prevSnapshot[userId];
                  const after = currentSnapshot[userId];
                  if (!before || !after) continue;
                  const deltas: string[] = [];
                  (Object.keys(before) as Array<keyof PlayerSnapshot>).forEach((stat) => {
                    const delta = Number(after[stat] ?? 0) - Number(before[stat] ?? 0);
                    if (!Number.isFinite(delta) || delta === 0) return;
                    deltas.push(`${formatSigned(delta)} ${STAT_NAME_RU[stat]}`);
                  });
                  if (deltas.length === 0) continue;
                  const info = getPlayerColorInfo(userId, playersRef.current);
                  summaryByPlayer.push(`${info?.title ?? 'Игрок'}: ${deltas.join(', ')}`);
                }
                if (summaryByPlayer.length > 0) {
                  batch.push({
                    text: `Итоги хода: ${summaryByPlayer.join(' | ')}`,
                    kind: 'success',
                    turn: Math.max(1, historyTurnRef.current - 1),
                  });
                }
              }

              prevSnapshotRef.current = currentSnapshot;
              prevActivePlayerRef.current = currentActivePlayerId;

              if (batch.length > 0) {
                appendFrontLogEntries(batch);
              }

              const unityState = toUnityGameStatePayload(rawState, playersRef.current);
              setGameState(unityState);
            }
            break;
          case 'room.state':
            setPlayers(normalizeRoomPlayers(data.payload));
            break;
          case 'game.started':
            showToast('Игра началась!', 'success');
            pushFrontLog({ text: 'Игра началась.', kind: 'success', turn: historyTurnRef.current });
            break;
          case 'game.dice_rolled':
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnDiceRolled', data.payload.value);
            }
            break;
          case 'game.move':
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnPlayerMove', JSON.stringify(data.payload));
            }
            break;
          case 'game.card':
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnCardPlayed', JSON.stringify(data.payload));
            }
            break;
          case 'game.card_closed':
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnOwnerCardClosed', JSON.stringify(data.payload));
            }
            break;
          case 'game.project':
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnProjectCompleted', JSON.stringify(data.payload));
            }
            break;
          case 'game.turn_changed':
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnTurnChanged', data.payload.activePlayerId);
            }
            break;
          case 'game.turn_timer':
            setTurnTimer({
              running: Boolean(data.payload.running),
              durationMs: Number(data.payload.durationMs ?? 0),
              remainingMs: Number(data.payload.remainingMs ?? 0),
              endsAt: Number(data.payload.endsAt ?? 0),
              activePlayerId:
                typeof data.payload.activePlayerId === 'string' ? data.payload.activePlayerId : null,
            });
            setTimerNowMs(Date.now());
            break;
          case 'game.token_moved':
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnTokenMoved', JSON.stringify(data.payload));
            }
            break;
          case 'game.finished':
            showToast('Игра завершена', 'success');
            pushFrontLog({ text: 'Игра завершена.', kind: 'success', turn: historyTurnRef.current });
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnGameFinished', JSON.stringify(data.payload));
            }
            break;
          case 'game.paused':
            if (!isPausedRef.current) {
              showToast(`Игра на паузе: ${data.payload.reason}`, 'info');
              pushFrontLog({
                text: `Пауза: ${data.payload.reason}`,
                kind: 'warn',
                turn: historyTurnRef.current,
              });
            }
            isPausedRef.current = true;
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnGamePaused', JSON.stringify(data.payload));
            }
            break;
          case 'game.resumed':
            if (isPausedRef.current) {
              showToast('Игра возобновлена', 'success');
              pushFrontLog({ text: 'Игра возобновлена.', kind: 'success', turn: historyTurnRef.current });
            }
            isPausedRef.current = false;
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnGameResumed', JSON.stringify(data.payload));
            }
            break;
          case 'error':
            {
              const code = String(data.payload?.message ?? '');
              const localizedMessage = translateServerError(code);
              showToast(localizedMessage, 'error');
              pushFrontLog({ text: localizedMessage, kind: 'error', turn: historyTurnRef.current });
              if (isLoadedRef.current) {
                sendMessage(
                  'GameManager',
                  'OnServerError',
                  JSON.stringify({
                    code,
                    message: localizedMessage,
                    playSadSound: SAD_SOUND_ERROR_CODES.has(code) || code.includes('TRAVEL'),
                  }),
                );
              }
            }
            break;
        }
      } catch (e) {
        console.error('Error parsing WS message:', e);
      }
    };

    ws.onerror = (error) => {
      console.error('Game WebSocket error:', error);
    };

    ws.onclose = () => {
      console.log('Game WebSocket disconnected');
    };

    return () => {
      ws.onopen = null;
      ws.onmessage = null;
      ws.onerror = null;
      ws.onclose = null;
      if (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING) {
        ws.close();
      }
      if (wsRef.current === ws) {
        wsRef.current = null;
      }
    };
  }, [appendFrontLogEntries, id, roomPassword, pushFrontLog, sendMessage, showToast]);

  // Каркас bridge Unity -> WebSocket intents.
  // Unity может вызывать эти события через JS bridge:
  // - ws.game.roll_dice()
  // - ws.game.move(jsonPayload)
  // - ws.game.card(jsonPayload)
  // - ws.game.project(jsonPayload)
  // - ws.game.character_select(jsonPayload)
  // - ws.game.green_choice(jsonPayload)
  useEffect(() => {
    const onRollDice = () => {
      sendWsMessage({ type: 'game.roll_dice', payload: {} });
    };

    const onMove = (rawPayload?: string) => {
      try {
        const payload = rawPayload ? JSON.parse(rawPayload) : {};
        const steps = Number(payload.steps);
        const toSectorValue = payload.toSector;
        const toSector =
          toSectorValue === undefined || toSectorValue === null ? undefined : Number(toSectorValue);

        if (!Number.isFinite(steps) || steps < 1 || steps > 6) {
          showToast('Unity прислал некорректный ход: steps', 'error');
          return;
        }
        if (toSector !== undefined && !Number.isInteger(toSector)) {
          showToast('Unity прислал некорректный ход: toSector', 'error');
          return;
        }

        sendWsMessage({
          type: 'game.move',
          payload: { steps, toSector },
        });
      } catch (error) {
        console.error('Invalid Unity move payload', error);
        showToast('Unity прислал невалидный JSON для game.move', 'error');
      }
    };

    const onCard = (rawPayload?: string) => {
      try {
        const payload = rawPayload ? JSON.parse(rawPayload) : {};
        const cardId =
          typeof payload.cardId === 'string' && payload.cardId.trim().length > 0
            ? payload.cardId
            : undefined;
        sendWsMessage({
          type: 'game.card',
          payload: {
            cardId,
          },
        });
      } catch (error) {
        console.error('Invalid Unity card payload', error);
        showToast('Unity прислал невалидный JSON для game.card', 'error');
      }
    };

    const onProject = (rawPayload?: string) => {
      try {
        const payload = rawPayload ? JSON.parse(rawPayload) : {};
        const projectId =
          typeof payload.projectId === 'string' && payload.projectId.trim().length > 0
            ? payload.projectId
            : undefined;
        const successPointsRaw = payload.successPoints;
        const successPoints =
          successPointsRaw === undefined || successPointsRaw === null
            ? undefined
            : Number(successPointsRaw);
        if (successPoints !== undefined && (!Number.isFinite(successPoints) || successPoints < 0)) {
          showToast('Unity прислал некорректный successPoints', 'error');
          return;
        }

        sendWsMessage({
          type: 'game.project',
          payload: {
            projectId,
            successPoints,
          },
        });
      } catch (error) {
        console.error('Invalid Unity project payload', error);
        showToast('Unity прислал невалидный JSON для game.project', 'error');
      }
    };

    const onCardClosed = (rawPayload?: string) => {
      try {
        const payload = rawPayload ? JSON.parse(rawPayload) : {};
        const playerId =
          typeof payload.playerId === 'string' && payload.playerId.trim().length > 0
            ? payload.playerId
            : undefined;
        const cardId =
          typeof payload.cardId === 'string' && payload.cardId.trim().length > 0
            ? payload.cardId
            : undefined;
        if (!playerId || !cardId) {
          showToast('Unity прислал некорректный game.card_closed', 'error');
          return;
        }
        sendWsMessage({
          type: 'game.card_closed',
          payload: { playerId, cardId },
        });
      } catch (error) {
        console.error('Invalid Unity card_closed payload', error);
        showToast('Unity прислал невалидный JSON для game.card_closed', 'error');
      }
    };

    const onCharacterSelect = (rawPayload?: string) => {
      try {
        const payload = rawPayload ? JSON.parse(rawPayload) : {};
        const characterId =
          typeof payload.characterId === 'string' && payload.characterId.trim().length > 0
            ? payload.characterId
            : undefined;
        const statsRaw = payload.stats ?? {};
        const toInt = (value: unknown) => {
          const n = Number(value);
          return Number.isFinite(n) ? Math.trunc(n) : 0;
        };
        sendWsMessage({
          type: 'game.character_select',
          payload: {
            characterId,
            stats: {
              money: toInt(statsRaw.money),
              experience: toInt(statsRaw.experience),
              success: toInt(statsRaw.success),
              volounteer: toInt(statsRaw.volounteer),
              science: toInt(statsRaw.science),
              art: toInt(statsRaw.art),
              media: toInt(statsRaw.media),
              business: toInt(statsRaw.business),
              sport: toInt(statsRaw.sport),
              tourism: toInt(statsRaw.tourism),
              it: toInt(statsRaw.it),
            },
          },
        });
      } catch (error) {
        console.error('Invalid Unity character_select payload', error);
        showToast('Unity прислал невалидный JSON для game.character_select', 'error');
      }
    };

    const onGreenChoice = (rawPayload?: string) => {
      try {
        const payload = rawPayload ? JSON.parse(rawPayload) : {};
        const cardId =
          typeof payload.cardId === 'string' && payload.cardId.trim().length > 0
            ? payload.cardId
            : undefined;
        const partnerUserId =
          typeof payload.partnerUserId === 'string' && payload.partnerUserId.trim().length > 0
            ? payload.partnerUserId
            : undefined;
        if (!cardId) {
          showToast('Unity прислал некорректный game.green_choice (cardId)', 'error');
          return;
        }
        sendWsMessage({
          type: 'game.green_choice',
          payload: {
            cardId,
            partnerUserId,
          },
        });
      } catch (error) {
        console.error('Invalid Unity green_choice payload', error);
        showToast('Unity прислал невалидный JSON для game.green_choice', 'error');
      }
    };

    addEventListener('ws.game.roll_dice', onRollDice);
    addEventListener('ws.game.move', onMove);
    addEventListener('ws.game.card', onCard);
    addEventListener('ws.game.project', onProject);
    addEventListener('ws.game.card_closed', onCardClosed);
    addEventListener('ws.game.character_select', onCharacterSelect);
    addEventListener('ws.game.green_choice', onGreenChoice);

    return () => {
      removeEventListener('ws.game.roll_dice', onRollDice);
      removeEventListener('ws.game.move', onMove);
      removeEventListener('ws.game.card', onCard);
      removeEventListener('ws.game.project', onProject);
      removeEventListener('ws.game.card_closed', onCardClosed);
      removeEventListener('ws.game.character_select', onCharacterSelect);
      removeEventListener('ws.game.green_choice', onGreenChoice);
    };
  }, [addEventListener, removeEventListener, sendWsMessage, showToast]);

  useEffect(() => {
    if (!isLoaded || !gameState) return;
    try {
      sendMessage('GameManager', 'UpdateGameState', JSON.stringify(gameState));
    } catch (e) {
      console.error('Ошибка UpdateGameState в Unity:', e);
    }
  }, [isLoaded, gameState, sendMessage]);

  useEffect(() => {
    if (!isLoaded || !localUserId) return;
    try {
      sendMessage('GameManager', 'SetLocalUserId', localUserId);
    } catch (e) {
      console.error('Ошибка SetLocalUserId в Unity:', e);
    }
  }, [isLoaded, localUserId, sendMessage]);

  // Когда Unity загрузился и есть игроки, отправляем bootstrap один раз.
  useEffect(() => {
    if (!isLoaded || players.length === 0 || !id) return;

    const initKey = `${id}:${players.map((p) => p.userId).join(',')}:${localUserId ?? ''}`;
    if (unityInitKeyRef.current === initKey) {
      return;
    }
    unityInitKeyRef.current = initKey;

    unityBootstrapTimeoutsRef.current.forEach((tid) => window.clearTimeout(tid));
    unityBootstrapTimeoutsRef.current = [];
    try {
      sendMessage('GameManager', 'SetPlayerCount', players.length);
      if (localUserId) {
        sendMessage('GameManager', 'SetLocalUserId', localUserId);
      }
      players.forEach((player, index) => {
        const safeName = typeof player.username === 'string' && player.username.trim().length > 0
          ? player.username
          : `Игрок ${index + 1}`;
        sendMessage('GameManager', 'SetPlayerName', safeName);
        sendMessage('GameManager', 'SetPlayerId', player.userId);
      });
    } catch (e) {
      console.error('Ошибка bootstrap в Unity:', e);
    }

    return () => {
      unityBootstrapTimeoutsRef.current.forEach((tid) => window.clearTimeout(tid));
      unityBootstrapTimeoutsRef.current = [];
    };
  }, [id, isLoaded, players, sendMessage, localUserId]);

  // Выход из игры
  const handleExitGame = async () => {
    try {
      await api.leaveRoom(id!);
      showToast('Вы вышли из игры', 'info');
      if (wsRef.current?.readyState === WebSocket.OPEN) {
        wsRef.current.close();
      }
      await unload();
      navigate('/rooms');
    } catch (err) {
      console.error('Ошибка при выходе:', err);
      showToast('Не удалось выйти из игры', 'error');
      navigate('/rooms');
    }
  };

  // Повторная попытка загрузки Unity
  const handleRetry = () => {
    window.location.reload();
  };

  // Полноэкранный режим
  const toggleFullscreen = useCallback(() => {
    const target = fullscreenRootRef.current;
    if (!target) return;

    if (!isFullscreen) {
      target.requestFullscreen?.().catch((e) => {
        console.error('Не удалось включить полноэкранный режим', e);
      });
    } else {
      if (document.exitFullscreen) {
        document.exitFullscreen();
      }
    }
  }, [isFullscreen]);

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, []);

  // Прогресс загрузки в процентах
  const loadingPercent = Math.round(loadingProgression * 100);
  const timerDurationMs = Math.max(0, Number(turnTimer?.durationMs ?? 0));
  const timerRemainingMs = turnTimer?.running
    ? Math.max(0, (turnTimer?.endsAt ?? 0) - timerNowMs)
    : Math.max(0, Number(turnTimer?.remainingMs ?? 0));
  const timerProgress = timerDurationMs > 0 ? Math.min(1, timerRemainingMs / timerDurationMs) : 0;
  const ringRadius = 34;
  const ringLength = 2 * Math.PI * ringRadius;
  const timerVisible = Boolean(
    turnTimer &&
      timerDurationMs > 0 &&
      gameState?.started &&
      !gameState?.isPaused
  );
  const timerIsCharacterSelection =
    timerVisible &&
    Array.isArray(gameState?.players) &&
    gameState.players.some((p: any) => !p?.selectedCharacterId);
  const timerTitle = timerIsCharacterSelection ? 'Выбор персонажа' : 'Время хода';
  const timerActivePlayerLabel =
    turnTimer?.activePlayerId && !timerIsCharacterSelection ? getPlayerLabel(turnTimer.activePlayerId) : 'Общий таймер';

  if (loading) {
    return <Loader text="Загрузка данных комнаты..." fullPage />;
  }

  return (
    <div className="game-container" ref={fullscreenRootRef}>
      <div className="game-header">
        <Button variant="outline" onClick={handleExitGame}>
          ← Выйти из игры
        </Button>
        <h1>Игровая комната</h1>
        <Button 
          variant="outline" 
          onClick={toggleFullscreen}
          className="fullscreen-button"
        >
          {isFullscreen ? '⤢ Выйти из полноэкранного' : '⤢ Полноэкранный режим'}
        </Button>
      </div>

      <div className="players-count-header">
        Игроков: {players.length}
      </div>

      {timerVisible && (
        <div className="turn-timer-card" aria-live="polite">
          <div className="turn-timer-ring">
            <svg viewBox="0 0 84 84" className="turn-timer-svg" role="img" aria-label="Таймер хода">
              <circle className="turn-timer-track" cx="42" cy="42" r={ringRadius} />
              <circle
                className={`turn-timer-progress${timerRemainingMs <= 5000 ? ' danger' : ''}`}
                cx="42"
                cy="42"
                r={ringRadius}
                strokeDasharray={ringLength}
                strokeDashoffset={ringLength * (1 - timerProgress)}
              />
            </svg>
            <div className="turn-timer-time">{formatTimerSeconds(timerRemainingMs)}</div>
          </div>
          <div className="turn-timer-text">
            <div className="turn-timer-title">{timerTitle}</div>
            <div className="turn-timer-player">{timerActivePlayerLabel}</div>
          </div>
        </div>
      )}

      {!isFullscreen && (
        <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '0.75rem' }}>
          <Button variant="outline" onClick={() => setShowDevPanel((v) => !v)}>
            {showDevPanel ? 'Скрыть панель отладки' : 'Показать панель отладки'}
          </Button>
        </div>
      )}

      <button className="front-log-fab" onClick={() => setIsLogOpen((v) => !v)}>
        {isLogOpen ? 'Скрыть журнал' : 'Журнал'}
      </button>

      {showDevPanel && !isFullscreen && (
        <div style={{ marginBottom: '1rem', padding: '0.75rem', border: '1px solid #d1d5db', borderRadius: 8, background: '#f9fafb' }}>
          <div style={{ fontWeight: 600, marginBottom: '0.5rem' }}>Панель отладки: изменение параметров через сервер</div>
          <div style={{ display: 'grid', gap: '0.5rem' }}>
            <label>
              Режим:
              <select
                style={{ marginLeft: 8 }}
                value={devAdjustMode}
                onChange={(e) => setDevAdjustMode(e.target.value as DevAdjustMode)}
              >
                <option value="delta">Изменить на (+/-)</option>
                <option value="set">Задать точные значения</option>
              </select>
            </label>
            <label>
              Игрок:
              <select
                style={{ marginLeft: 8 }}
                value={devTargetUserId}
                onChange={(e) => setDevTargetUserId(e.target.value)}
              >
                {players.map((p) => (
                  <option key={p.userId} value={p.userId}>
                    {getPlayerLabel(p.userId)}
                  </option>
                ))}
              </select>
            </label>
            <div style={{ fontSize: 12, color: '#4b5563' }}>
              {devAdjustMode === 'delta'
                ? 'Введите дельты: +N / -N к текущим значениям.'
                : 'Введите целевые числа: сервер выставит эти значения (с учетом лимитов).'}
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, minmax(120px, 1fr))', gap: '0.5rem' }}>
              {Object.keys(INITIAL_DEV_DELTAS).map((key) => (
                <label key={key} style={{ display: 'grid', gap: 4 }}>
                  <span>{STAT_NAME_RU[key] ?? key}</span>
                  <input
                    type="number"
                    value={(devDeltas as any)[key]}
                    onChange={(e) =>
                      setDevDeltas((prev) => ({
                        ...prev,
                        [key]: Number(e.target.value || 0),
                      }))
                    }
                  />
                </label>
              ))}
            </div>
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              <Button variant="primary" onClick={applyDevAdjust}>Применить</Button>
              <Button variant="outline" onClick={() => setDevDeltas({ ...INITIAL_DEV_DELTAS })}>Сбросить</Button>
            </div>
          </div>
        </div>
      )}

      {isLogOpen && (
        <div
          className="front-log-panel floating"
          style={{ left: logPos.x, top: logPos.y, width: logPanelSize.width, height: logPanelSize.height }}
        >
          <div
            className="front-log-header draggable"
            onMouseDown={(e) => {
              draggingLogRef.current = true;
              dragStartRef.current = { x: e.clientX, y: e.clientY };
            }}
          >
            <span>Журнал игры</span>
            <div className="front-log-actions" onMouseDown={(e) => e.stopPropagation()}>
              <Button variant="outline" onClick={() => setFrontLog([])}>Очистить</Button>
              <Button variant="outline" onClick={() => setIsLogOpen(false)}>Закрыть</Button>
            </div>
          </div>
          <div className="front-log-list">
            {frontLog.length === 0 ? (
              <div className="front-log-empty">Пока нет событий</div>
            ) : (
              frontLog.map((entry) => (
                <div key={entry.id} className={`front-log-item ${entry.kind}`}>
                  <span className="front-log-time">
                    {new Date(entry.at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                  </span>
                  <span className="front-log-turn">Ход {entry.turn}</span>
                  <span className={`front-log-actor ${entry.actorColor ? `color-${entry.actorColor}` : ''}`}>
                    {entry.actorTitle ? `${entry.actorTitle}:` : 'Система:'}
                  </span>
                  <span>{entry.text}</span>
                </div>
              ))
            )}
          </div>
          <div
            className="front-log-resizer"
            onMouseDown={(e) => {
              e.preventDefault();
              e.stopPropagation();
              resizingLogRef.current = true;
              resizeStartRef.current = {
                x: e.clientX,
                y: e.clientY,
                width: logPanelSize.width,
                height: logPanelSize.height,
              };
            }}
          />
        </div>
      )}

      <div 
        className="game-content" 
        ref={gameContainerRef}
      >
        {!isLoaded && (
          <div className="unity-loading">
            <div className="progress-bar-container">
              <div 
                className="progress-bar-fill" 
                style={{ width: `${loadingPercent}%` }}
              />
            </div>
            <p>Загрузка игры... {loadingPercent}%</p>
            <div className="loader-spinner-small" />
            <Button 
              variant="outline" 
              onClick={handleRetry}
              style={{ marginTop: '1rem' }}
            >
              Перезагрузить
            </Button>
          </div>
        )}
        
        <Unity 
          unityProvider={unityProvider} 
          style={{ 
            width: "100%", 
            height: isFullscreen ? "100%" : "600px",
            border: isFullscreen ? "none" : "2px solid #d1fae5",
            borderRadius: isFullscreen ? "0" : "12px",
            display: isLoaded ? 'block' : 'none'
          }} 
        />
      </div>
    </div>
  );
};

export default Game;