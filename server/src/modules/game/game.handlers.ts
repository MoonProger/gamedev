import { WsIn, WsOut } from "../ws/ws.types";
import { prisma } from "../../db/prisma";
import { getOrCreateGame, setGame } from "./game.state";
import { loadGameState, saveGameState } from "./game.persistence";
import { finalizeGame } from "./game.results";
import { getBoardSectorById, getBoardStartSector, isMoveReachable, isStrictBoardValidationEnabled } from "./game.board";
import { getCardDecks, mapNodeTypeToDeckKey, ServerCard } from "./game.cards";
import { getCharacterTemplates } from "./game.characters";

function randDice() {
  return 1 + Math.floor(Math.random() * 6);
}

type RoomTurnTimerState = {
  timeout: NodeJS.Timeout;
  signature: string;
  durationMs: number;
  endsAt: number;
  activePlayerId: string;
  mode: "turn" | "card";
};
type CharacterSelectionTimerState = {
  timeout: NodeJS.Timeout;
  deadlineAt: number;
};

const turnTimers = new Map<string, RoomTurnTimerState>();
const characterSelectionTimers = new Map<string, CharacterSelectionTimerState>();
const SURPRISE_CARD_TYPE = 0;
const CHARACTER_SELECTION_TIMEOUT_MS = 90_000;

function parseRoomSettings(raw: unknown): { timerSeconds?: number } {
  if (!raw) return {};
  if (typeof raw === "string") {
    try {
      return JSON.parse(raw);
    } catch {
      return {};
    }
  }
  if (typeof raw === "object") return raw as { timerSeconds?: number };
  return {};
}

function getTurnTimerDurationMs(_room: any, game: any): number | null {
  if (!game?.started) return null;
  if (getPendingCardWin(game) || getPendingCardClose(game) || getPendingGreenChoice(game)) {
    return 60_000;
  }
  return 30_000;
}

function buildTurnSignature(game: any): string {
  return [
    game?.started ? "1" : "0",
    game?.isPaused ? "1" : "0",
    String(game?.activePlayerId ?? ""),
    String(game?.phase ?? ""),
    String(game?.lastDice ?? ""),
    String(Array.isArray(game?.history) ? game.history.length : 0),
  ].join("|");
}

function clearTurnTimer(roomId: string) {
  const existing = turnTimers.get(roomId);
  if (!existing) return;
  clearTimeout(existing.timeout);
  turnTimers.delete(roomId);
}

function clearCharacterSelectionTimer(roomId: string) {
  const existing = characterSelectionTimers.get(roomId);
  if (!existing) return;
  clearTimeout(existing.timeout);
  characterSelectionTimers.delete(roomId);
}

export function clearTurnTimerForRoom(roomId: string) {
  clearTurnTimer(roomId);
  clearCharacterSelectionTimer(roomId);
}

function emitTurnTimerState(
  roomId: string,
  game: any,
  broadcast: (roomId: string, msg: WsOut) => void,
  timer: { running: boolean; durationMs: number; remainingMs: number; endsAt: number }
) {
  broadcast(roomId, {
    type: "game.turn_timer",
    payload: {
      running: timer.running,
      durationMs: Math.max(0, Math.trunc(timer.durationMs)),
      remainingMs: Math.max(0, Math.trunc(timer.remainingMs)),
      endsAt: Math.max(0, Math.trunc(timer.endsAt)),
      activePlayerId: typeof game?.activePlayerId === "string" ? game.activePlayerId : null,
    },
  } as any);
}

function ensurePlayerState(game: any, userId: string) {
  if (game.positions[userId] === undefined) game.positions[userId] = getBoardStartSector();
  if (!game.scores[userId]) game.scores[userId] = {};
  if (game.money[userId] === undefined) game.money[userId] = 0;
  if (game.experience[userId] === undefined) game.experience[userId] = 0;
  if (!game.deckState[userId]) {
    game.deckState[userId] = {
      grants: 0,
      completedProjects: {},
      milestoneClaims: {},
    };
  }
  if (!game.deckState[userId].milestoneClaims) game.deckState[userId].milestoneClaims = {};
}

function nextTurn(roomPlayers: { userId: string }[], currentUserId: string) {
  const idx = roomPlayers.findIndex((p) => p.userId === currentUserId);
  return roomPlayers[(idx + 1) % roomPlayers.length].userId;
}

const PROJECT_SPHERES = [
  "volounteer",
  "science",
  "art",
  "media",
  "business",
  "sport",
  "tourism",
  "it",
] as const;
const SPHERE_SET = new Set<string>(PROJECT_SPHERES as unknown as string[]);

function mapStatEnumToKey(statName: number, currentDeckKey: string): string | null {
  switch (statName) {
    case 0: // CurrentSphere
      return currentDeckKey;
    case 1:
      return "money";
    case 2:
      return "experience";
    case 3:
      return "success";
    case 4:
      return "volounteer";
    case 5:
      return "science";
    case 6:
      return "art";
    case 7:
      return "media";
    case 8:
      return "business";
    case 9:
      return "sport";
    case 10:
      return "tourism";
    case 11:
      return "it";
    case 12: // LastSphere (fallback to current sphere for now)
      return currentDeckKey;
    default:
      return null;
  }
}

function clampByStat(statKey: string, value: number): number {
  if (!Number.isFinite(value)) return 0;
  if (statKey === "money") return Math.max(0, Math.trunc(value));
  if (statKey === "experience") return Math.max(0, Math.min(10, Math.trunc(value)));
  if (statKey === "success") return Math.max(0, Math.min(12, Math.trunc(value)));
  if (SPHERE_SET.has(statKey)) return Math.max(0, Math.min(10, Math.trunc(value)));
  return Math.trunc(value);
}

function setStatAbsolute(game: any, userId: string, statKey: string, absolute: number): number {
  ensurePlayerState(game, userId);
  const next = clampByStat(statKey, absolute);
  if (statKey === "money") {
    const prev = Number(game.money[userId] ?? 0);
    game.money[userId] = next;
    return next - prev;
  }
  if (statKey === "experience") {
    const prev = Number(game.experience[userId] ?? 0);
    game.experience[userId] = next;
    return next - prev;
  }
  const prev = Number(game.scores[userId][statKey] ?? 0);
  game.scores[userId][statKey] = next;
  return next - prev;
}

function applyStatDelta(game: any, userId: string, statKey: string, delta: number): number {
  if (!Number.isFinite(delta) || delta === 0) return 0;
  ensurePlayerState(game, userId);
  if (statKey === "money") {
    const prev = Number(game.money[userId] ?? 0);
    const next = clampByStat("money", prev + delta);
    game.money[userId] = next;
    return next - prev;
  }
  if (statKey === "experience") {
    const prev = Number(game.experience[userId] ?? 0);
    const next = clampByStat("experience", prev + delta);
    game.experience[userId] = next;
    return next - prev;
  }
  const prev = Number(game.scores[userId][statKey] ?? 0);
  const next = clampByStat(statKey, prev + delta);
  game.scores[userId][statKey] = next;
  return next - prev;
}

function getStatValue(game: any, userId: string, statKey: string): number {
  if (statKey === "money") return Number(game.money[userId] ?? 0);
  if (statKey === "experience") return Number(game.experience[userId] ?? 0);
  return Number(game.scores[userId]?.[statKey] ?? 0);
}

function applyStatDeltaTracked(
  game: any,
  userId: string,
  statKey: string,
  delta: number,
  deltasByUser: Map<string, Array<{ stat: string; delta: number }>>
) {
  if (!Number.isFinite(delta) || delta === 0) return;
  const applied = applyStatDelta(game, userId, statKey, delta);
  if (applied === 0) return;
  if (!deltasByUser.has(userId)) deltasByUser.set(userId, []);
  deltasByUser.get(userId)!.push({ stat: statKey, delta: applied });
  if (SPHERE_SET.has(statKey)) {
    const bonus = applySphereMilestonesForUser(game, userId);
    if (bonus !== 0) deltasByUser.get(userId)!.push({ stat: "success", delta: bonus });
  }
}

function applySphereMilestonesForUser(game: any, userId: string): number {
  const deckState = getPlayerDeckState(game, userId);
  const claims = deckState.milestoneClaims ?? {};
  deckState.milestoneClaims = claims;
  let totalBonus = 0;

  for (const sphere of PROJECT_SPHERES) {
    const level = Number(game.scores[userId]?.[sphere] ?? 0);
    const key5 = `${sphere}_5`;
    const key10 = `${sphere}_10`;
    if (level >= 5 && !claims[key5]) {
      claims[key5] = true;
      totalBonus += 2;
    }
    if (level >= 10 && !claims[key10]) {
      claims[key10] = true;
      totalBonus += 1;
    }
  }

  if (totalBonus !== 0) return applyStatDelta(game, userId, "success", totalBonus);
  return 0;
}

function getGreenChoiceCandidates(
  game: any,
  roomPlayers: { userId: string }[],
  userId: string,
  card: ServerCard,
  currentDeckKey: string
) {
  const soloLeader = card.soloEffects[0];
  const soloPartner = card.soloEffects[1];
  const coopLeader = card.coopEffects[0];
  const coopPartner = card.coopEffects[1];

  const resolve = (eff: any) => (eff ? mapStatEnumToKey(eff.statName, currentDeckKey) : null);
  const coopPartnerStat = resolve(coopPartner);

  const myPartnerLevel = coopPartnerStat ? getStatValue(game, userId, coopPartnerStat) : 0;
  const candidates = roomPlayers
    .map((p, idx) => ({ userId: p.userId, idx }))
    .filter((p) => p.userId !== userId && coopPartnerStat && getStatValue(game, p.userId, coopPartnerStat) >= myPartnerLevel);

  const orderedIds = [userId, ...candidates.map((c) => c.userId)];
  return Array.from(new Set(orderedIds));
}

function applyGreenCardServerSide(
  game: any,
  roomPlayers: { userId: string }[],
  userId: string,
  card: ServerCard,
  currentDeckKey: string,
  deltasByUser: Map<string, Array<{ stat: string; delta: number }>>,
  partnerChoiceUserId?: string
) {
  const soloLeader = card.soloEffects[0];
  const soloPartner = card.soloEffects[1];
  const coopLeader = card.coopEffects[0];
  const coopPartner = card.coopEffects[1];

  const resolve = (eff: any) => (eff ? mapStatEnumToKey(eff.statName, currentDeckKey) : null);
  const soloLeaderStat = resolve(soloLeader);
  const soloPartnerStat = resolve(soloPartner);
  const coopLeaderStat = resolve(coopLeader);
  const coopPartnerStat = resolve(coopPartner);

  const candidateIds = getGreenChoiceCandidates(game, roomPlayers, userId, card, currentDeckKey);
  const chosenPartnerId =
    partnerChoiceUserId && candidateIds.includes(partnerChoiceUserId) && partnerChoiceUserId !== userId
      ? partnerChoiceUserId
      : null;

  if (!chosenPartnerId) {
    if (soloLeader && soloLeaderStat)
      applyStatDeltaTracked(game, userId, soloLeaderStat, Number(soloLeader.amount) || 0, deltasByUser);
    if (soloPartner && soloPartnerStat)
      applyStatDeltaTracked(game, userId, soloPartnerStat, Number(soloPartner.amount) || 0, deltasByUser);
    return { mode: "solo", partnerId: null };
  }

  if (coopLeader && coopLeaderStat)
    applyStatDeltaTracked(game, userId, coopLeaderStat, Number(coopLeader.amount) || 0, deltasByUser);
  if (coopPartner && coopPartnerStat)
    applyStatDeltaTracked(game, chosenPartnerId, coopPartnerStat, Number(coopPartner.amount) || 0, deltasByUser);
  return { mode: "coop", partnerId: chosenPartnerId, candidateIds };
}

function applyCardAndCollectDeltas(
  game: any,
  roomPlayers: { userId: string }[],
  userId: string,
  card: ServerCard,
  currentDeckKey: string,
  greenPartnerChoiceUserId?: string
) {
  const deltasByUser = new Map<string, Array<{ stat: string; delta: number }>>();
  const dice2d6 = randDice() + randDice();
  const currentSphereLevel = game.scores[userId]?.[currentDeckKey] ?? 0;
  const blueSuccess = (game.experience[userId] ?? 0) > dice2d6;
  const redSuccess = currentSphereLevel >= 5;
  let greenResolution: { mode: string; partnerId: string | null } | null = null;

  if (card.cardType === 4) {
    greenResolution = applyGreenCardServerSide(
      game,
      roomPlayers,
      userId,
      card,
      currentDeckKey,
      deltasByUser,
      greenPartnerChoiceUserId
    );
  } else {
    const effectsToApply = card.effects;

    for (const eff of effectsToApply) {
      let shouldApply = eff.condition === 0;
      if (eff.condition === 1) {
        shouldApply = card.cardType === 2 ? blueSuccess : card.cardType === 3 ? redSuccess : true;
      } else if (eff.condition === 2) {
        shouldApply = card.cardType === 2 ? !blueSuccess : card.cardType === 3 ? !redSuccess : false;
      }
      if (!shouldApply) continue;

      if (eff.effect !== 2 && eff.effect !== 3) continue;
      const statKey = mapStatEnumToKey(eff.statName, currentDeckKey);
      if (!statKey) continue;

      const rawAmount = Number(eff.amount) || 0;
      const delta = eff.effect === 3 ? -Math.abs(rawAmount) : rawAmount;
      if (!delta) continue;

      applyStatDeltaTracked(game, userId, statKey, delta, deltasByUser);
    }
  }

  const actingPlayerDeltas = deltasByUser.get(userId) ?? [];

  return {
    deltas: actingPlayerDeltas,
    deltasByUser,
    checks: {
      blueDiceSum: card.cardType === 2 ? dice2d6 : null,
      blueSuccess: card.cardType === 2 ? blueSuccess : null,
      blueExperience: card.cardType === 2 ? Number(game.experience[userId] ?? 0) : null,
      redSuccess: card.cardType === 3 ? redSuccess : null,
      redSphereLevel: card.cardType === 3 ? Number(currentSphereLevel ?? 0) : null,
      redRequiredLevel: card.cardType === 3 ? 5 : null,
      greenMode: greenResolution?.mode ?? null,
      greenPartnerId: greenResolution?.partnerId ?? null,
    },
  };
}

function getPlayerDeckState(game: any, userId: string): {
  grants: number;
  completedProjects: Record<string, boolean>;
  milestoneClaims: Record<string, boolean>;
  selectedCharacter?: {
    characterId: string;
    selectedAt: string;
    byUserId: string;
  };
} {
  ensurePlayerState(game, userId);
  if (!game.deckState[userId].milestoneClaims) game.deckState[userId].milestoneClaims = {};
  return game.deckState[userId];
}

function chooseProjectSphere(game: any, userId: string, requestedProjectId?: string): string | null {
  const state = getPlayerDeckState(game, userId);
  const candidates = PROJECT_SPHERES.filter((sphere) => {
    const level = Number(game.scores[userId]?.[sphere] ?? 0);
    return level >= 10 && !state.completedProjects[sphere];
  });
  if (candidates.length === 0) return null;
  if (!requestedProjectId) return candidates[0];
  const normalized = requestedProjectId.trim().toLowerCase();
  return candidates.includes(normalized as any) ? normalized : null;
}

function hasGrantEligibleSphere(game: any, userId: string): boolean {
  return PROJECT_SPHERES.some((sphere) => Number(game.scores[userId]?.[sphere] ?? 0) >= 10);
}

function buildPlayerStateSnapshot(game: any, userId: string) {
  const scores = game.scores[userId] ?? {};
  return {
    money: Number(game.money[userId] ?? 0),
    experience: Number(game.experience[userId] ?? 0),
    success: Number(scores["success"] ?? 0),
    volounteer: Number(scores["volounteer"] ?? 0),
    science: Number(scores["science"] ?? 0),
    art: Number(scores["art"] ?? 0),
    media: Number(scores["media"] ?? 0),
    business: Number(scores["business"] ?? 0),
    sport: Number(scores["sport"] ?? 0),
    tourism: Number(scores["tourism"] ?? 0),
    it: Number(scores["it"] ?? 0),
  };
}

function buildAffectedPlayersSnapshot(game: any, affectedPlayerIds: string[]) {
  return affectedPlayerIds.map((pid) => ({
    playerId: pid,
    playerState: buildPlayerStateSnapshot(game, pid),
  }));
}

type PendingCardWin = {
  winnerUserId: string;
  ownerUserId: string;
  cardId: string;
};

type PendingCardClose = {
  ownerUserId: string;
  cardId: string;
  nextActivePlayerId: string;
};

type PendingGreenChoice = {
  ownerUserId: string;
  cardId: string;
  deckKey: string;
  card: ServerCard;
  candidateUserIds: string[];
};

function getMetaDeckState(game: any): any {
  if (!game.deckState) game.deckState = {};
  if (!game.deckState.__meta) game.deckState.__meta = {};
  return game.deckState.__meta;
}

function hasCharacterSelection(game: any, userId: string): boolean {
  const state = game?.deckState?.[userId];
  return Boolean(state?.selectedCharacter?.characterId);
}

function isCharacterSelectionCompleted(game: any): boolean {
  const meta = getMetaDeckState(game);
  return Boolean(meta.characterSelectionCompleted);
}

function markCharacterSelectionCompleted(game: any) {
  const meta = getMetaDeckState(game);
  meta.characterSelectionCompleted = true;
  delete meta.characterSelectionDeadlineAt;
}

function ensureCharacterSelectionInitialized(game: any) {
  const meta = getMetaDeckState(game);
  if (!meta.characterSelectionDeadlineAt) {
    meta.characterSelectionDeadlineAt = Date.now() + CHARACTER_SELECTION_TIMEOUT_MS;
  }
  if (meta.characterSelectionCompleted === undefined) {
    meta.characterSelectionCompleted = false;
  }
}

function allPlayersSelected(game: any, roomPlayers: { userId: string }[]) {
  return roomPlayers.every((p) => hasCharacterSelection(game, p.userId));
}


function applyCharacterSelection(game: any, userId: string, selected: any, stats: any) {
  ensurePlayerState(game, userId);
  const deckState = getPlayerDeckState(game, userId);
  const toInt = (value: unknown) => {
    const n = Number(value);
    return Number.isFinite(n) ? Math.trunc(n) : 0;
  };
  deckState.selectedCharacter = {
    characterId:
      typeof selected?.characterId === "string" && selected.characterId.trim().length > 0
        ? selected.characterId
        : "client_character",
    selectedAt:
      typeof selected?.selectedAt === "string" && selected.selectedAt.trim().length > 0
        ? selected.selectedAt
        : new Date().toISOString(),
    byUserId:
      typeof selected?.byUserId === "string" && selected.byUserId.trim().length > 0
        ? selected.byUserId
        : userId,
  };
  deckState.grants = 0;
  deckState.completedProjects = {};
  deckState.milestoneClaims = {};

  game.money[userId] = clampByStat("money", toInt(stats.money));
  game.experience[userId] = clampByStat("experience", toInt(stats.experience));
  game.scores[userId]["success"] = clampByStat("success", toInt(stats.success));
  game.scores[userId]["volounteer"] = clampByStat("volounteer", toInt(stats.volounteer));
  game.scores[userId]["science"] = clampByStat("science", toInt(stats.science));
  game.scores[userId]["art"] = clampByStat("art", toInt(stats.art));
  game.scores[userId]["media"] = clampByStat("media", toInt(stats.media));
  game.scores[userId]["business"] = clampByStat("business", toInt(stats.business));
  game.scores[userId]["sport"] = clampByStat("sport", toInt(stats.sport));
  game.scores[userId]["tourism"] = clampByStat("tourism", toInt(stats.tourism));
  game.scores[userId]["it"] = clampByStat("it", toInt(stats.it));
}

function resetCardDrawMeta(game: any) {
  const meta = getMetaDeckState(game);
  delete meta.lastDrawnCardType;
}

function drawCardWithAntiSurpriseStreak(game: any, deckKey: string): ServerCard | null {
  const deck = getCardDecks()[deckKey];
  if (!deck || deck.length === 0) return null;

  const meta = getMetaDeckState(game);
  const previousType = Number(meta.lastDrawnCardType);
  const banSurpriseNow = previousType === SURPRISE_CARD_TYPE;

  const eligible = banSurpriseNow
    ? deck.filter((card) => Number(card.cardType) !== SURPRISE_CARD_TYPE)
    : deck;
  const pool = eligible.length > 0 ? eligible : deck;

  const selected = pool[Math.floor(Math.random() * pool.length)];
  meta.lastDrawnCardType = Number(selected.cardType);
  return selected;
}

function findAutoMoveTarget(fromSector: number, steps: number): number | undefined {
  if (!isStrictBoardValidationEnabled()) return undefined;

  const fromType = normalizeNodeType(getBoardSectorById(fromSector)?.nodeType);
  if (fromType === "money") return fromSector;

  const dfs = (current: number, stepsLeft: number, visited: Set<number>): number | null => {
    if (stepsLeft === 0) return current;
    const neighbors = [...(getBoardSectorById(current)?.neighbors ?? [])].sort((a, b) => a - b);
    for (const next of neighbors) {
      if (visited.has(next)) continue;
      visited.add(next);
      const candidate = dfs(next, stepsLeft - 1, visited);
      visited.delete(next);
      if (candidate !== null) return candidate;
    }
    return null;
  };

  const result = dfs(fromSector, steps, new Set<number>([fromSector]));
  return result ?? undefined;
}

function getPendingCardWin(game: any): PendingCardWin | null {
  const meta = getMetaDeckState(game);
  const pending = meta.pendingCardWin;
  if (!pending) return null;
  if (
    typeof pending.winnerUserId !== "string" ||
    typeof pending.ownerUserId !== "string" ||
    typeof pending.cardId !== "string"
  ) {
    return null;
  }
  return pending as PendingCardWin;
}

function setPendingCardWin(game: any, pending: PendingCardWin) {
  const meta = getMetaDeckState(game);
  meta.pendingCardWin = pending;
}

function clearPendingCardWin(game: any) {
  const meta = getMetaDeckState(game);
  delete meta.pendingCardWin;
}

function getPendingCardClose(game: any): PendingCardClose | null {
  const meta = getMetaDeckState(game);
  const pending = meta.pendingCardClose;
  if (!pending) return null;
  if (
    typeof pending.ownerUserId !== "string" ||
    typeof pending.cardId !== "string" ||
    typeof pending.nextActivePlayerId !== "string"
  ) {
    return null;
  }
  return pending as PendingCardClose;
}

function setPendingCardClose(game: any, pending: PendingCardClose) {
  const meta = getMetaDeckState(game);
  meta.pendingCardClose = pending;
}

function clearPendingCardClose(game: any) {
  const meta = getMetaDeckState(game);
  delete meta.pendingCardClose;
}

function getPendingGreenChoice(game: any): PendingGreenChoice | null {
  const meta = getMetaDeckState(game);
  const pending = meta.pendingGreenChoice;
  if (!pending) return null;
  if (
    typeof pending.ownerUserId !== "string" ||
    typeof pending.cardId !== "string" ||
    typeof pending.deckKey !== "string" ||
    typeof pending.card !== "object" ||
    !Array.isArray(pending.candidateUserIds)
  ) {
    return null;
  }
  return pending as PendingGreenChoice;
}

function setPendingGreenChoice(game: any, pending: PendingGreenChoice) {
  const meta = getMetaDeckState(game);
  meta.pendingGreenChoice = pending;
}

function clearPendingGreenChoice(game: any) {
  const meta = getMetaDeckState(game);
  delete meta.pendingGreenChoice;
}

function pickWinnerAtSuccess12(roomPlayers: { userId: string }[], game: any): string | null {
  for (const p of roomPlayers) {
    const success = Number(game.scores?.[p.userId]?.success ?? 0);
    if (success >= 12) return p.userId;
  }
  return null;
}

async function finalizeAndBroadcastWin(
  roomId: string,
  winnerUserId: string,
  game: any,
  broadcast: (roomId: string, msg: WsOut) => void
) {
  clearCharacterSelectionTimer(roomId);
  clearPendingCardWin(game);
  clearPendingCardClose(game);
  clearPendingGreenChoice(game);
  await finalizeGame(roomId, winnerUserId, game);

  game.started = false;
  game.phase = "WAITING_ROLL";
  game.lastDice = null;

  await saveGameState(roomId, game);

  broadcast(roomId, {
    type: "game.finished",
    payload: {
      winnerUserId,
      finalScores: game.scores,
    },
  } as any);

  broadcast(roomId, {
    type: "game.state",
    payload: game,
  } as any);
}

async function advanceTurnWithActionError(
  roomId: string,
  roomPlayers: { userId: string }[],
  game: any,
  userId: string,
  message: string,
  historyType: string
) {
  game.history.push({
    type: historyType,
    playerId: userId,
    reason: message,
    at: new Date().toISOString(),
  });

  const next = nextTurn(roomPlayers, userId);
  game.activePlayerId = next;
  game.lastDice = null;
  game.phase = "WAITING_ROLL";

  await saveGameState(roomId, game);

  return { next };
}

function normalizeNodeType(nodeType?: string): string {
  return typeof nodeType === "string" ? nodeType.trim().toLowerCase() : "";
}

async function resolveCharacterSelectionProgress(
  roomId: string,
  roomPlayers: { userId: string }[],
  game: any,
  broadcast: (roomId: string, msg: WsOut) => void
) {
  ensureCharacterSelectionInitialized(game);
  if (isCharacterSelectionCompleted(game)) return;

  // Авто-выбор ТОЛЬКО для ботов из существующих персонажей
  const templates = getCharacterTemplates();
  const usedCharacterIds = new Set<string>();

  // Сначала собираем уже выбранных персонажей у реальных игроков
  for (const p of roomPlayers) {
    if (!p.userId.startsWith("bot_") && hasCharacterSelection(game, p.userId)) {
      const charId = game.deckState[p.userId]?.selectedCharacter?.characterId;
      if (charId) usedCharacterIds.add(charId);
    }
  }

  // Выбираем для ботов
  for (const p of roomPlayers) {
    if (p.userId.startsWith("bot_") && !hasCharacterSelection(game, p.userId)) {
      const available = templates.filter(t => !usedCharacterIds.has(t.id));
      if (available.length > 0) {
        const randomIndex = Math.floor(Math.random() * available.length);
        const template = available[randomIndex];
        applyCharacterSelection(game, p.userId, { characterId: template.id }, {
          money: template.money,
          experience: template.experience,
          success: template.success,
          volounteer: template.volounteer,
          science: template.science,
          art: template.art,
          media: template.media,
          business: template.business,
          sport: template.sport,
          tourism: template.tourism,
          it: template.it,
        });
        usedCharacterIds.add(template.id);
        game.history.push({
          type: "character_select_auto",
          playerId: p.userId,
          characterId: template.id,
          at: new Date().toISOString(),
        });
        console.log(`Auto-selected character ${template.id} for bot ${p.userId}`);
      }
    }
  }

  // Проверяем, все ли реальные игроки выбрали персонажа
  const realPlayers = roomPlayers.filter(p => !p.userId.startsWith("bot_"));
  const allRealPlayersHaveCharacter = realPlayers.every(p => hasCharacterSelection(game, p.userId));

  if (!allRealPlayersHaveCharacter) {
    console.log(`Waiting for real players to select character: ${realPlayers.filter(p => !hasCharacterSelection(game, p.userId)).map(p => p.userId).join(', ')}`);
    return;
  }

  // Все реальные игроки выбрали, можно завершать выбор персонажа
  if (allPlayersSelected(game, roomPlayers)) {
    markCharacterSelectionCompleted(game);
    await saveGameState(roomId, game);
    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }
}

export async function runAutoTurnAction(ctx: {
  roomId: string;
  userId: string;
  broadcast: (roomId: string, msg: WsOut) => void;
}) {
  const { roomId, userId, broadcast } = ctx;
  const room = await prisma.room.findUnique({
    where: { id: roomId },
    include: { players: true },
  });
  if (!room || room.players.length === 0 || !room.players.some((p) => p.userId === userId)) {
    clearTurnTimer(roomId);
    return;
  }

  const game = getOrCreateGame(roomId);
  if (!game.started || game.isPaused || game.activePlayerId !== userId) return;

  const pendingCardWin = getPendingCardWin(game);
  if (pendingCardWin && pendingCardWin.ownerUserId === userId) {
    await handleGameMessage({
      roomId,
      userId,
      msg: {
        type: "game.card_closed",
        payload: { playerId: pendingCardWin.ownerUserId, cardId: pendingCardWin.cardId },
      },
      broadcast,
      reply: () => {},
    });
    return;
  }

  const pendingCardClose = getPendingCardClose(game);
  if (pendingCardClose && pendingCardClose.ownerUserId === userId) {
    await handleGameMessage({
      roomId,
      userId,
      msg: {
        type: "game.card_closed",
        payload: { playerId: pendingCardClose.ownerUserId, cardId: pendingCardClose.cardId },
      },
      broadcast,
      reply: () => {},
    });
    return;
  }

  const pendingGreenChoice = getPendingGreenChoice(game);
  if (pendingGreenChoice && pendingGreenChoice.ownerUserId === userId) {
    const choices = Array.isArray(pendingGreenChoice.candidateUserIds)
      ? pendingGreenChoice.candidateUserIds
      : [];
    const candidate = choices.length > 0 ? choices[Math.floor(Math.random() * choices.length)] : userId;
    const partnerUserId = candidate && candidate !== userId ? candidate : undefined;
    await handleGameMessage({
      roomId,
      userId,
      msg: {
        type: "game.green_choice",
        payload: { cardId: pendingGreenChoice.cardId, partnerUserId },
      },
      broadcast,
      reply: () => {},
    });
    return;
  }

  if (game.phase === "WAITING_ROLL") {
    await handleGameMessage({
      roomId,
      userId,
      msg: { type: "game.roll_dice", payload: {} },
      broadcast,
      reply: () => {},
    });
    return;
  }

  if (game.phase === "WAITING_MOVE") {
    const steps = Number(game.lastDice ?? 0);
    if (!Number.isFinite(steps) || steps < 1) return;
    const fromSector = Number(game.positions[userId] ?? getBoardStartSector());
    const toSector = findAutoMoveTarget(fromSector, steps);
    await handleGameMessage({
      roomId,
      userId,
      msg: {
        type: "game.move",
        payload: toSector === undefined ? { steps } : { steps, toSector },
      },
      broadcast,
      reply: () => {},
    });
    return;
  }

  if (game.phase === "WAITING_ACTION") {
    const currentSector = Number(game.positions[userId] ?? getBoardStartSector());
    const currentType = normalizeNodeType(getBoardSectorById(currentSector)?.nodeType);
    const autoMsg: WsIn =
      currentType === "project"
        ? { type: "game.project", payload: {} }
        : { type: "game.card", payload: {} };
    await handleGameMessage({
      roomId,
      userId,
      msg: autoMsg,
      broadcast,
      reply: () => {},
    });
  }
}

async function syncTurnTimerForRoomState(
  room: any,
  game: any,
  broadcast: (roomId: string, msg: WsOut) => void
) {
  const roomId = String(room?.id ?? "");
  if (!roomId) return;

  syncCharacterSelectionTimer(room, game, broadcast);
  const durationMs = getTurnTimerDurationMs(room, game);
  const mode: "turn" | "card" =
    getPendingCardWin(game) || getPendingCardClose(game) || getPendingGreenChoice(game) ? "card" : "turn";
  if (
    !durationMs ||
    !game?.started ||
    game?.isPaused ||
    !game?.activePlayerId ||
    !isCharacterSelectionCompleted(game)
  ) {
    const prev = turnTimers.get(roomId);
    clearTurnTimer(roomId);
    emitTurnTimerState(roomId, game, broadcast, {
      running: false,
      durationMs: durationMs ?? prev?.durationMs ?? 0,
      remainingMs: 0,
      endsAt: 0,
    });
    return;
  }

  const signature = buildTurnSignature(game);
  const existing = turnTimers.get(roomId);
  if (existing && existing.signature === signature && existing.durationMs === durationMs) {
    emitTurnTimerState(roomId, game, broadcast, {
      running: true,
      durationMs,
      remainingMs: Math.max(0, existing.endsAt - Date.now()),
      endsAt: existing.endsAt,
    });
    return;
  }

  clearTurnTimer(roomId);
  const activePlayerId = String(game.activePlayerId);
  const now = Date.now();
  let remainingMs = durationMs;
  if (existing) {
    const prevRemainingMs = Math.max(0, existing.endsAt - now);
    if (existing.activePlayerId === activePlayerId) {
      if (existing.mode === mode) {
        remainingMs = prevRemainingMs + 5000;
      } else if (mode === "card") {
        remainingMs = durationMs;
      } else {
        remainingMs = prevRemainingMs + 5000;
      }
    }
  }
  const endsAt = now + remainingMs;

  const timeout = setTimeout(async () => {
    const armed = turnTimers.get(roomId);
    if (!armed || armed.signature !== signature) return;
    armed.endsAt = Date.now();

    const current = getOrCreateGame(roomId);
    if (
      !current.started ||
      current.isPaused ||
      current.activePlayerId !== activePlayerId ||
      buildTurnSignature(current) !== signature
    ) {
      return;
    }

    await runAutoTurnAction({ roomId, userId: activePlayerId, broadcast });
  }, remainingMs);

  turnTimers.set(roomId, { timeout, signature, durationMs, endsAt, activePlayerId, mode });
  emitTurnTimerState(roomId, game, broadcast, {
    running: true,
    durationMs,
    remainingMs,
    endsAt,
  });
}

function syncCharacterSelectionTimer(
  room: any,
  game: any,
  broadcast: (roomId: string, msg: WsOut) => void
) {
  const roomId = String(room?.id ?? "");
  if (!roomId || !game?.started || isCharacterSelectionCompleted(game)) {
    clearCharacterSelectionTimer(roomId);
    return;
  }

  ensureCharacterSelectionInitialized(game);
  const deadlineAt = Number(getMetaDeckState(game).characterSelectionDeadlineAt ?? 0);
  if (!Number.isFinite(deadlineAt) || deadlineAt <= 0) return;

  emitTurnTimerState(roomId, game, broadcast, {
    running: true,
    durationMs: CHARACTER_SELECTION_TIMEOUT_MS,
    remainingMs: Math.max(0, deadlineAt - Date.now()),
    endsAt: deadlineAt,
  });

  const existing = characterSelectionTimers.get(roomId);
  if (existing && existing.deadlineAt === deadlineAt) return;
  clearCharacterSelectionTimer(roomId);

  const delay = Math.max(0, deadlineAt - Date.now());
  const timeout = setTimeout(async () => {
    characterSelectionTimers.delete(roomId);
    const liveRoom = await prisma.room.findUnique({
      where: { id: roomId },
      include: { players: true },
    });
    if (!liveRoom || liveRoom.players.length === 0) {
      clearTurnTimer(roomId);
      return;
    }
    const liveGame = getOrCreateGame(roomId);
    await resolveCharacterSelectionProgress(roomId, liveRoom.players, liveGame, broadcast);
    await syncTurnTimerForRoomState(liveRoom, liveGame, broadcast);
  }, delay);

  characterSelectionTimers.set(roomId, { timeout, deadlineAt });
}

export async function syncTurnTimerForRoom(
  roomId: string,
  broadcast: (roomId: string, msg: WsOut) => void
) {
  const room = await prisma.room.findUnique({
    where: { id: roomId },
    include: { players: true },
  });

  if (!room) {
    clearTurnTimerForRoom(roomId);
    return;
  }

  let game = getOrCreateGame(roomId);
  if (!game.started) {
    const persisted = await loadGameState(roomId);
    if (persisted?.started) {
      setGame(roomId, persisted as any);
      game = getOrCreateGame(roomId);
    }
  }

  await syncTurnTimerForRoomState(room, game, broadcast);
}

export async function handleGameMessage(ctx: {
  roomId: string;
  userId: string;
  msg: WsIn;
  broadcast: (roomId: string, msg: WsOut) => void;
  reply: (msg: WsOut) => void;
}) {
  const { roomId, userId, msg, broadcast, reply } = ctx;

  if (userId.startsWith("bot_")) {
  console.log(`Bot ${userId} sent message, server will handle bot automatically`);
  return;
}

  const sendError = (message: string) => {
    reply({ type: "error", payload: { message } } as any);
  };

  const room = await prisma.room.findUnique({
    where: { id: roomId },
    include: { players: true },
  });

  if (!room) {
    sendError("ROOM_NOT_FOUND");
    return;
  }

  const game = getOrCreateGame(roomId);
  
  try {
    if (msg.type === "game.start") {
  clearCharacterSelectionTimer(roomId);
  if (game.started) {
    sendError("GAME_ALREADY_STARTED");
    return;
  }

  const players = room.players;
  const allReady = players.length >= 2 && players.every((p) => p.isReady);

  if (!allReady) {
    sendError("NOT_ALL_READY_OR_TOO_FEW_PLAYERS");
    return;
  }

  // Собираем уже выбранных персонажей у реальных игроков
  const usedCharacterIds = new Set<string>();
  for (const p of players) {
    if (!p.userId.startsWith("bot_") && hasCharacterSelection(game, p.userId)) {
      const charId = game.deckState[p.userId]?.selectedCharacter?.characterId;
      if (charId) usedCharacterIds.add(charId);
    }
  }

  // Выбираем персонажей для ботов из существующих (getCharacterTemplates)
  const templates = getCharacterTemplates();
  for (const p of players) {
    if (p.userId.startsWith("bot_") && !hasCharacterSelection(game, p.userId)) {
      // Выбираем случайного персонажа из тех, кто ещё не занят
      const available = templates.filter(t => !usedCharacterIds.has(t.id));
      if (available.length > 0) {
        const randomIndex = Math.floor(Math.random() * available.length);
        const template = available[randomIndex];

        applyCharacterSelection(game, p.userId, { characterId: template.id }, {
          money: template.money,
          experience: template.experience,
          success: template.success,
          volounteer: template.volounteer,
          science: template.science,
          art: template.art,
          media: template.media,
          business: template.business,
          sport: template.sport,
          tourism: template.tourism,
          it: template.it,
        });

        usedCharacterIds.add(template.id);
        game.history.push({
          type: "character_select_auto",
          playerId: p.userId,
          characterId: template.id,
          at: new Date().toISOString(),
        });
        console.log(`Auto-selected character ${template.id} for bot ${p.userId}`);
      } else {
        console.log(`No available characters for bot ${p.userId}`);
      }
    }
  }

  // Настраиваем состояние игры (НО НЕ НАЧИНАЕМ!)
  game.started = false;
  game.characterSelectionRequired = true;
  game.isPaused = false;
  game.activePlayerId = null;
  game.lastDice = null;
  game.phase = "WAITING_ROLL";
  clearPendingCardWin(game);
  clearPendingCardClose(game);
  clearPendingGreenChoice(game);
  resetCardDrawMeta(game);
  ensureCharacterSelectionInitialized(game);

  for (const p of players) {
    ensurePlayerState(game, p.userId);
  }

  // Сохраняем состояние и ждём выбора персонажа от реальных игроков
  await saveGameState(roomId, game);

  // Отправляем всем состояние (чтобы клиенты показали выбор персонажа)
  broadcast(roomId, { type: "game.state", payload: game } as any);
  return;
}

    // ВЫБОР ПЕРСОНАЖА
    if (msg.type === "game.character_select") {
      if (hasCharacterSelection(game, userId)) {
        await saveGameState(roomId, game);
        broadcast(roomId, { type: "game.state", payload: game } as any);
        return;
      }

      const selected = {
        characterId:
          typeof msg.payload.characterId === "string" && msg.payload.characterId.trim().length > 0
            ? msg.payload.characterId
            : "client_character",
        selectedAt: new Date().toISOString(),
        byUserId: userId,
      };
      applyCharacterSelection(game, userId, selected, msg.payload.stats ?? {});

      game.history.push({
        type: "character_select",
        playerId: userId,
        characterId: selected.characterId,
        at: selected.selectedAt,
      });

      await saveGameState(roomId, game);

      // Проверяем, все ли реальные игроки выбрали персонажа
      const realPlayers = room.players.filter(p => !p.userId.startsWith("bot_"));
      const allRealPlayersHaveCharacter = realPlayers.every(p => hasCharacterSelection(game, p.userId));

    if (allRealPlayersHaveCharacter && !game.started) {
    // ✅ ВСЕ ВЫБРАЛИ — ЗАПУСКАЕМ ИГРУ!
    game.started = true;
    game.characterSelectionRequired = false;
    game.activePlayerId = room.players[0].userId;
    game.phase = "WAITING_ROLL";
    markCharacterSelectionCompleted(game);

        await prisma.room.update({
          where: { id: roomId },
          data: { status: "IN_GAME" },
        }).catch(() => {});

        await saveGameState(roomId, game);

        broadcast(roomId, {
          type: "game.started",
          payload: { activePlayerId: game.activePlayerId },
        } as any);
        broadcast(roomId, { type: "game.state", payload: game } as any);
      } else {
        // Ждём остальных
        broadcast(roomId, { type: "game.state", payload: game } as any);
      }
      return;
    }

    // Обработка ходов только если игра началась
    if (!game.started) {
      sendError("GAME_NOT_STARTED_YET");
      return;
    }

    if (game.isPaused) {
      sendError("GAME_PAUSED");
      return;
    }

    if (game.activePlayerId !== userId) {
      sendError("NOT_YOUR_TURN");
      return;
    }

    ensurePlayerState(game, userId);

    // ============= ОСТАЛЬНЫЕ ОБРАБОТЧИКИ ХОДОВ =============
    if (msg.type === "game.roll_dice") {
      if (game.phase !== "WAITING_ROLL") {
        sendError("WRONG_PHASE");
        return;
      }

      const value = randDice();
      game.lastDice = value;
      game.phase = "WAITING_MOVE";

      game.history.push({
        type: "roll_dice",
        playerId: userId,
        dice: value,
        at: new Date().toISOString(),
      });

      await saveGameState(roomId, game);

      broadcast(roomId, { type: "game.dice_rolled", payload: { value } } as any);
      broadcast(roomId, { type: "game.state", payload: game } as any);
      return;
    }

    if (msg.type === "game.move") {
      if (game.phase !== "WAITING_MOVE" || !game.lastDice) {
        sendError("ROLL_DICE_FIRST");
        return;
      }

      const steps = Number(msg.payload.steps);
      if (!Number.isFinite(steps) || steps < 1 || steps > 6) {
        sendError("INVALID_STEPS");
        return;
      }

      if (steps !== game.lastDice) {
        sendError("STEPS_MUST_EQUAL_DICE");
        return;
      }

      const fromSector = game.positions[userId] ?? getBoardStartSector();
      const rolledDice = Number(game.lastDice);
      let toSector = fromSector + steps;

      if (isStrictBoardValidationEnabled()) {
        const requestedToSector = Number(msg.payload.toSector);
        if (!Number.isInteger(requestedToSector) || requestedToSector < 0) {
          sendError("MOVE_TARGET_REQUIRED");
          return;
        }

        const fromSectorType = normalizeNodeType(getBoardSectorById(fromSector)?.nodeType);
        const isMoneyStayMove =
          fromSectorType === "money" && requestedToSector === fromSector && steps === rolledDice;
        const valid = isMoneyStayMove || isMoveReachable(fromSector, requestedToSector, steps);
        if (!valid) {
          sendError("INVALID_MOVE_PATH");
          return;
        }

        toSector = requestedToSector;
      }

      game.positions[userId] = toSector;
      game.phase = "WAITING_ACTION";

      game.history.push({
        type: "move",
        playerId: userId,
        fromSector,
        toSector,
        dice: game.lastDice,
        at: new Date().toISOString(),
      });

      const destinationSector = getBoardSectorById(toSector);
      const destinationType = normalizeNodeType(destinationSector?.nodeType);
      const isAutoResolvedSector = destinationType === "money" || destinationType === "none";
      let nextPlayerAfterAutoResolve: string | null = null;
      if (destinationType === "money") {
        const rewarded = applyStatDelta(game, userId, "money", 1);
        game.history.push({
          type: "money_reward",
          playerId: userId,
          amount: rewarded,
          sector: toSector,
          at: new Date().toISOString(),
        });
      }
      if (isAutoResolvedSector) {
        game.history.push({
          type: "empty_action",
          playerId: userId,
          sector: toSector,
          at: new Date().toISOString(),
        });
        nextPlayerAfterAutoResolve = nextTurn(room.players, userId);
        game.activePlayerId = nextPlayerAfterAutoResolve;
        game.lastDice = null;
        game.phase = "WAITING_ROLL";
      }

      await saveGameState(roomId, game);

      broadcast(roomId, {
        type: "game.move",
        payload: {
          playerId: userId,
          fromSector,
          toSector,
          dice: rolledDice,
        },
      } as any);

      broadcast(roomId, {
        type: "game.token_moved",
        payload: { playerId: userId, pos: toSector, steps },
      } as any);

      if (nextPlayerAfterAutoResolve) {
        broadcast(roomId, {
          type: "game.turn_changed",
          payload: { activePlayerId: nextPlayerAfterAutoResolve },
        } as any);
      }

      broadcast(roomId, { type: "game.state", payload: game } as any);
      return;
    }

    if (msg.type === "game.card") {
      if (game.phase !== "WAITING_ACTION") {
        sendError("WRONG_PHASE_FOR_CARD");
        return;
      }

      const currentSector = game.positions[userId] ?? getBoardStartSector();
      const boardSector = getBoardSectorById(currentSector);
      const deckKey = mapNodeTypeToDeckKey(boardSector?.nodeType);
      if (!deckKey) {
        const nodeType = normalizeNodeType(boardSector?.nodeType);
        if (nodeType === "money" || nodeType === "none") {
          if (nodeType === "money") {
            const rewarded = applyStatDelta(game, userId, "money", 1);
            game.history.push({
              type: "money_reward",
              playerId: userId,
              amount: rewarded,
              sector: currentSector,
              at: new Date().toISOString(),
            });
          }

          game.history.push({
            type: "empty_action",
            playerId: userId,
            sector: currentSector,
            at: new Date().toISOString(),
          });

          const next = nextTurn(room.players, userId);
          game.activePlayerId = next;
          game.lastDice = null;
          game.phase = "WAITING_ROLL";

          await saveGameState(roomId, game);

          broadcast(roomId, {
            type: "game.turn_changed",
            payload: { activePlayerId: next },
          } as any);
          broadcast(roomId, { type: "game.state", payload: game } as any);
          return;
        }
        sendError("CARD_DECK_NOT_FOUND_FOR_SECTOR");
        return;
      }

      if (deckKey === "grant_success" && !hasGrantEligibleSphere(game, userId)) {
        game.history.push({
          type: "grant_unavailable",
          playerId: userId,
          reason: "GRANT_REQUIRES_LEVEL_10_SPHERE",
          at: new Date().toISOString(),
        });

        const next = nextTurn(room.players, userId);
        game.activePlayerId = next;
        game.lastDice = null;
        game.phase = "WAITING_ROLL";

        await saveGameState(roomId, game);

        sendError("GRANT_REQUIRES_LEVEL_10_SPHERE");

        broadcast(roomId, {
          type: "game.turn_changed",
          payload: { activePlayerId: next },
        } as any);

        broadcast(roomId, { type: "game.state", payload: game } as any);
        return;
      }

      const card = drawCardWithAntiSurpriseStreak(game, deckKey);
      if (!card) {
        sendError("CARD_DECK_EMPTY");
        return;
      }

      let travelPaymentDelta = 0;
      if (deckKey === "travel") {
        const currentMoney = Number(game.money[userId] ?? 0);
        if (currentMoney < 1) {
          const { next } = await advanceTurnWithActionError(
            roomId,
            room.players,
            game,
            userId,
            "TRAVEL_NOT_ENOUGH_MONEY",
            "travel_unavailable"
          );
          sendError("TRAVEL_NOT_ENOUGH_MONEY");
          broadcast(roomId, {
            type: "game.turn_changed",
            payload: { activePlayerId: next },
          } as any);
          broadcast(roomId, { type: "game.state", payload: game } as any);
          return;
        }

        travelPaymentDelta = applyStatDelta(game, userId, "money", -1);
        game.history.push({
          type: "travel_paid",
          playerId: userId,
          amount: travelPaymentDelta,
          at: new Date().toISOString(),
        });
      }

      if (card.cardType === 4) {
        const candidateUserIds = getGreenChoiceCandidates(game, room.players, userId, card, deckKey);
        const autoResolveGreen = candidateUserIds.length <= 1;
        setPendingGreenChoice(game, {
          ownerUserId: userId,
          cardId: card.id,
          deckKey,
          card,
          candidateUserIds,
        });

        if (autoResolveGreen) {
          await saveGameState(roomId, game);
          await handleGameMessage({
            roomId,
            userId,
            msg: {
              type: "game.green_choice",
              payload: { cardId: card.id },
            },
            broadcast,
            reply: (message) => {
              if (message.type === "error") {
                sendError(String((message as { payload?: { message?: string } }).payload?.message ?? "GREEN_CHOICE_REQUIRED"));
              }
            },
          });
          return;
        }

        game.history.push({
          type: "card_green_pending",
          playerId: userId,
          requestedCardId: msg.payload.cardId,
          resolvedCardId: card.id,
          deckKey,
          candidates: candidateUserIds,
          at: new Date().toISOString(),
        });

        await saveGameState(roomId, game);
        broadcast(roomId, {
          type: "game.card",
          payload: {
            playerId: userId,
            cardId: card.id,
            cardType: card.cardType,
            imageGuid: card.imageGuid,
            deckKey,
            deltas: travelPaymentDelta !== 0 ? [{ stat: "money", delta: travelPaymentDelta }] : [],
            chainedCards: [],
            checks: {
              greenMode: "pending",
            },
            greenChoiceRequired: true,
            affectedPlayers: buildAffectedPlayersSnapshot(game, candidateUserIds),
            grants: getPlayerDeckState(game, userId).grants,
            scores: game.scores[userId],
            money: game.money[userId],
            experience: game.experience[userId],
            playerState: buildPlayerStateSnapshot(game, userId),
          },
        } as any);
        broadcast(roomId, { type: "game.state", payload: game } as any);
        return;
      }

      let primaryResult = applyCardAndCollectDeltas(game, room.players, userId, card, deckKey);
      if (travelPaymentDelta !== 0) {
        const existing = primaryResult.deltasByUser.get(userId) ?? [];
        const nextDeltas = [{ stat: "money", delta: travelPaymentDelta }, ...existing];
        primaryResult.deltasByUser.set(userId, nextDeltas);
        primaryResult = {
          ...primaryResult,
          deltas: nextDeltas,
        };
      }
      const chainedCards: Array<{
        cardId: string;
        cardType: number;
        imageGuid: string | null;
        deltas: Array<{ stat: string; delta: number }>;
      }> = [];

      const playerDeckState = getPlayerDeckState(game, userId);
      if (deckKey === "grant_success") {
        const grantDiceSum = randDice() + randDice();
        const grantSuccess = (game.experience[userId] ?? 0) > grantDiceSum;
        if (grantSuccess) {
          playerDeckState.grants = Math.max(0, Number(playerDeckState.grants ?? 0)) + 1;
          const successDelta = applyStatDelta(game, userId, "success", 1);
          if (successDelta !== 0) {
            const existing = primaryResult.deltasByUser.get(userId) ?? [];
            primaryResult.deltasByUser.set(userId, [...existing, { stat: "success", delta: successDelta }]);
          }
        }

        primaryResult = {
          ...primaryResult,
          deltas: primaryResult.deltasByUser.get(userId) ?? [],
          checks: {
            ...primaryResult.checks,
            grantDiceSum,
            grantSuccess,
          } as any,
        };
      }

      const hasChainDraw = card.effects.some((e) => e.effect === 4);
      if (hasChainDraw) {
        const bonus = drawCardWithAntiSurpriseStreak(game, deckKey);
        if (bonus) {
          const bonusResult = applyCardAndCollectDeltas(game, room.players, userId, bonus, deckKey);
          for (const [pid, deltas] of bonusResult.deltasByUser.entries()) {
            const existing = primaryResult.deltasByUser.get(pid) ?? [];
            primaryResult.deltasByUser.set(pid, [...existing, ...deltas]);
          }
          chainedCards.push({
            cardId: bonus.id,
            cardType: bonus.cardType,
            imageGuid: bonus.imageGuid,
            deltas: bonusResult.deltas,
          });
        }
      }

      const affectedPlayerIds = Array.from(primaryResult.deltasByUser.keys());
      if (!affectedPlayerIds.includes(userId)) affectedPlayerIds.push(userId);

      game.history.push({
        type: "card",
        playerId: userId,
        requestedCardId: msg.payload.cardId,
        resolvedCardId: card.id,
        deckKey,
        deltas: primaryResult.deltas,
        checks: primaryResult.checks,
        affectedPlayers: affectedPlayerIds,
        chainedCards,
        grants: playerDeckState.grants,
        at: new Date().toISOString(),
      });

      const winnerUserId = pickWinnerAtSuccess12(room.players, game);
      if (winnerUserId) {
        setPendingCardWin(game, {
          winnerUserId,
          ownerUserId: userId,
          cardId: card.id,
        });
        await saveGameState(roomId, game);
        broadcast(roomId, {
          type: "game.card",
          payload: {
            playerId: userId,
            cardId: card.id,
            cardType: card.cardType,
            imageGuid: card.imageGuid,
            deckKey,
            deltas: primaryResult.deltas,
            chainedCards,
            checks: primaryResult.checks,
            affectedPlayers: buildAffectedPlayersSnapshot(game, affectedPlayerIds),
            grants: playerDeckState.grants,
            scores: game.scores[userId],
            money: game.money[userId],
            experience: game.experience[userId],
            playerState: buildPlayerStateSnapshot(game, userId),
          },
        } as any);
        return;
      }

      const next = nextTurn(room.players, userId);
      setPendingCardClose(game, {
        ownerUserId: userId,
        cardId: card.id,
        nextActivePlayerId: next,
      });

      await saveGameState(roomId, game);

      broadcast(roomId, {
        type: "game.card",
        payload: {
          playerId: userId,
          cardId: card.id,
          cardType: card.cardType,
          imageGuid: card.imageGuid,
          deckKey,
          deltas: primaryResult.deltas,
          chainedCards,
          checks: primaryResult.checks,
          affectedPlayers: buildAffectedPlayersSnapshot(game, affectedPlayerIds),
          grants: playerDeckState.grants,
          scores: game.scores[userId],
          money: game.money[userId],
          experience: game.experience[userId],
          playerState: buildPlayerStateSnapshot(game, userId),
        },
      } as any);

      broadcast(roomId, { type: "game.state", payload: game } as any);
      return;
    }

    if (msg.type === "game.project") {
      if (game.phase !== "WAITING_ACTION") {
        sendError("WRONG_PHASE_FOR_PROJECT");
        return;
      }

      const currentSector = game.positions[userId] ?? getBoardStartSector();
      const boardSector = getBoardSectorById(currentSector);
      if (!boardSector || boardSector.nodeType?.toLowerCase() !== "project") {
        sendError("PROJECT_ACTION_NOT_ALLOWED_ON_THIS_SECTOR");
        return;
      }

      const chosenProjectSphere = chooseProjectSphere(game, userId, msg.payload.projectId);
      if (!chosenProjectSphere) {
        const { next } = await advanceTurnWithActionError(
          roomId,
          room.players,
          game,
          userId,
          "NO_AVAILABLE_PROJECTS",
          "project_unavailable"
        );
        sendError("NO_AVAILABLE_PROJECTS");
        broadcast(roomId, {
          type: "game.turn_changed",
          payload: { activePlayerId: next },
        } as any);
        broadcast(roomId, { type: "game.state", payload: game } as any);
        return;
      }

      const playerDeckState = getPlayerDeckState(game, userId);
      const grants = Math.max(0, Number(playerDeckState.grants ?? 0));
      const money = Math.max(0, Number(game.money[userId] ?? 0));
      if (grants <= 0 && money < 5) {
        const { next } = await advanceTurnWithActionError(
          roomId,
          room.players,
          game,
          userId,
          "NOT_ENOUGH_RESOURCES_FOR_PROJECT",
          "project_no_resources"
        );
        sendError("NOT_ENOUGH_RESOURCES_FOR_PROJECT");
        broadcast(roomId, {
          type: "game.turn_changed",
          payload: { activePlayerId: next },
        } as any);
        broadcast(roomId, { type: "game.state", payload: game } as any);
        return;
      }

      let payment: "grant" | "money";
      if (grants > 0) {
        playerDeckState.grants = grants - 1;
        payment = "grant";
      } else {
        applyStatDelta(game, userId, "money", -5);
        payment = "money";
      }

      playerDeckState.completedProjects[chosenProjectSphere] = true;

      const successPoints = applyStatDelta(game, userId, "success", 5);

      game.history.push({
        type: "project",
        playerId: userId,
        projectId: chosenProjectSphere,
        successPoints,
        payment,
        at: new Date().toISOString(),
      });

      const next = nextTurn(room.players, userId);
      game.activePlayerId = next;
      game.lastDice = null;
      game.phase = "WAITING_ROLL";

      await saveGameState(roomId, game);

      const totalSuccess = Number(game.scores[userId]["success"] ?? 0);

      broadcast(roomId, {
        type: "game.project",
        payload: {
          playerId: userId,
          projectId: chosenProjectSphere,
          successPoints,
          totalSuccess,
          payment,
          grants: playerDeckState.grants,
          money: game.money[userId],
          completedProjects: Object.keys(playerDeckState.completedProjects),
          playerState: buildPlayerStateSnapshot(game, userId),
        },
      } as any);

      const winnerUserId = pickWinnerAtSuccess12(room.players, game);
      if (winnerUserId) {
        await finalizeAndBroadcastWin(roomId, winnerUserId, game, broadcast);
        return;
      }

      broadcast(roomId, {
        type: "game.turn_changed",
        payload: { activePlayerId: next },
      } as any);

      broadcast(roomId, { type: "game.state", payload: game } as any);
      return;
    }
  } finally {
    await syncTurnTimerForRoomState(room, game, broadcast);
  }
}