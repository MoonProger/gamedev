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

function translateServerError(message: string): string {
  const key = String(message ?? '');
  return SERVER_ERROR_RU[key] ?? key;
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
  const [showDevPanel, setShowDevPanel] = useState(false);
  const [devTargetUserId, setDevTargetUserId] = useState<string>('');
  const [devDeltas, setDevDeltas] = useState<DevDeltaState>(INITIAL_DEV_DELTAS);
  const [devAdjustMode, setDevAdjustMode] = useState<DevAdjustMode>('delta');
  const isPausedRef = useRef(false);
  const playersRef = useRef<Player[]>([]);
  const isLoadedRef = useRef(false);
  const unityInitKeyRef = useRef<string | null>(null);
  const gameContainerRef = useRef<HTMLDivElement>(null);
  const wsRef = useRef<WebSocket | null>(null);
  const { showToast } = useToast();
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
      `Dev: ${devAdjustMode === 'set' ? 'установлены значения' : 'применены изменения'} для ${getPlayerLabel(devTargetUserId)}`,
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
        console.log('Game WS message:', data);

        switch (data.type) {
          case 'connected':
            setLocalUserId(data.payload.userId);
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'SetLocalUserId', data.payload.userId);
            }
            break;
          case 'game.state':
            {
              const unityState = toUnityGameStatePayload(data.payload, playersRef.current);
              setGameState(unityState);
              if (isLoadedRef.current) {
                sendMessage('GameManager', 'UpdateGameState', JSON.stringify(unityState));
              }
            }
            break;
          case 'room.state':
            setPlayers(normalizeRoomPlayers(data.payload));
            break;
          case 'game.started':
            showToast('Игра началась!', 'success');
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
          case 'game.token_moved':
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnTokenMoved', JSON.stringify(data.payload));
            }
            break;
          case 'game.finished':
            showToast('Игра завершена', 'success');
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnGameFinished', JSON.stringify(data.payload));
            }
            break;
          case 'game.paused':
            if (!isPausedRef.current) {
              showToast(`Игра на паузе: ${data.payload.reason}`, 'info');
            }
            isPausedRef.current = true;
            if (isLoadedRef.current) {
              sendMessage('GameManager', 'OnGamePaused', JSON.stringify(data.payload));
            }
            break;
          case 'game.resumed':
            if (isPausedRef.current) {
              showToast('Игра возобновлена', 'success');
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
  }, [id, roomPassword, sendMessage, showToast]);

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

  // Когда Unity загрузился и есть игроки, отправляем данные
  useEffect(() => {
    if (isLoaded && players.length > 0 && id) {
      const initKey = `${id}:${players.map((p) => p.userId).join(',')}`;
      if (unityInitKeyRef.current === initKey) {
        return;
      }
      unityInitKeyRef.current = initKey;
      console.log('Unity готов, отправляем данные игроков');
      
      setTimeout(() => {
        try {
          sendMessage('GameManager', 'SetPlayerCount', players.length);
          if (localUserId) {
            sendMessage('GameManager', 'SetLocalUserId', localUserId);
          }
          
          players.forEach((player, index) => {
            setTimeout(() => {
              const safeName = typeof player.username === 'string' && player.username.trim().length > 0
                ? player.username
                : `Player ${index + 1}`;
              sendMessage('GameManager', 'SetPlayerName', safeName);
              sendMessage('GameManager', 'SetPlayerId', player.userId);
            }, index * 200);
          });
          
          // Если есть сохранённое состояние игры, отправляем его
          if (gameState) {
            setTimeout(() => {
              sendMessage('GameManager', 'UpdateGameState', JSON.stringify(gameState));
            }, players.length * 200 + 500);
          }
        } catch (e) {
          console.error('Ошибка отправки в Unity:', e);
        }
      }, 1000);
    }
  }, [id, isLoaded, players, sendMessage, gameState, localUserId]);

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
    if (!gameContainerRef.current) return;

    if (!isFullscreen) {
      if (gameContainerRef.current.requestFullscreen) {
        gameContainerRef.current.requestFullscreen();
      }
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

  if (loading) {
    return <Loader text="Загрузка данных комнаты..." fullPage />;
  }

  return (
    <div className="game-container">
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

      <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '0.75rem' }}>
        <Button variant="outline" onClick={() => setShowDevPanel((v) => !v)}>
          {showDevPanel ? 'Скрыть Dev панель' : 'Показать Dev панель'}
        </Button>
      </div>

      {showDevPanel && (
        <div style={{ marginBottom: '1rem', padding: '0.75rem', border: '1px solid #d1d5db', borderRadius: 8, background: '#f9fafb' }}>
          <div style={{ fontWeight: 600, marginBottom: '0.5rem' }}>Dev панель: изменение статов через сервер</div>
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
                  <span>{key}</span>
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
            height: "600px", 
            border: "2px solid #d1fae5", 
            borderRadius: "12px",
            display: isLoaded ? 'block' : 'none'
          }} 
        />
      </div>
    </div>
  );
};

export default Game;