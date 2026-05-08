import { WsIn, WsOut } from "../ws/ws.types";
import { prisma } from "../../db/prisma";
import { getOrCreateGame } from "./game.state";
import { saveGameState } from "./game.persistence";
import { finalizeGame } from "./game.results";
import { getBoardSectorById, getBoardStartSector, isMoveReachable, isStrictBoardValidationEnabled } from "./game.board";
import { drawRandomCard, mapNodeTypeToDeckKey, ServerCard } from "./game.cards";

function randDice() {
  return 1 + Math.floor(Math.random() * 6);
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
      redSuccess: card.cardType === 3 ? redSuccess : null,
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
  clearPendingCardWin(game);
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

export async function handleGameMessage(ctx: {
  roomId: string;
  userId: string;
  msg: WsIn;
  broadcast: (roomId: string, msg: WsOut) => void;
  reply: (msg: WsOut) => void;
}) {
  const { roomId, userId, msg, broadcast, reply } = ctx;
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

  if (msg.type === "game.start") {
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

    game.started = true;
    game.isPaused = false;
    game.activePlayerId = players[0].userId;
    game.lastDice = null;
    game.phase = "WAITING_ROLL";
    clearPendingCardWin(game);
    clearPendingGreenChoice(game);

    for (const p of players) {
      ensurePlayerState(game, p.userId);
    }

    await saveGameState(roomId, game);

    broadcast(roomId, {
      type: "game.started",
      payload: { activePlayerId: game.activePlayerId },
    } as any);

    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }

  if (!game.started) {
    sendError("GAME_NOT_STARTED");
    return;
  }

  if (game.isPaused) {
    sendError("GAME_PAUSED");
    return;
  }

  if (msg.type === "game.character_select") {
    ensurePlayerState(game, userId);
    const toInt = (value: unknown) => {
      const n = Number(value);
      return Number.isFinite(n) ? Math.trunc(n) : 0;
    };
    const stats = msg.payload.stats ?? {};
    const selected = {
      characterId:
        typeof msg.payload.characterId === "string" && msg.payload.characterId.trim().length > 0
          ? msg.payload.characterId
          : "client_character",
      selectedAt: new Date().toISOString(),
      byUserId: userId,
    };
    const deckState = getPlayerDeckState(game, userId);
    deckState.selectedCharacter = selected;
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

    game.history.push({
      type: "character_select",
      playerId: userId,
      characterId: selected.characterId,
      at: selected.selectedAt,
    });

    await saveGameState(roomId, game);
    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }

  if (msg.type === "game.card_closed") {
    const pending = getPendingCardWin(game);
    broadcast(roomId, {
      type: "game.card_closed",
      payload: {
        playerId: msg.payload.playerId,
        cardId: msg.payload.cardId,
      },
    } as any);

    if (!pending) {
      return;
    }
    if (
      msg.payload.playerId !== pending.ownerUserId ||
      msg.payload.cardId !== pending.cardId ||
      userId !== pending.ownerUserId
    ) {
      sendError("INVALID_CARD_CLOSE_EVENT");
      return;
    }

    await finalizeAndBroadcastWin(roomId, pending.winnerUserId, game, broadcast);
    return;
  }

  if (msg.type === "game.green_choice") {
    const pendingGreen = getPendingGreenChoice(game);
    if (!pendingGreen) {
      sendError("NO_PENDING_GREEN_CHOICE");
      return;
    }
    if (userId !== pendingGreen.ownerUserId) {
      sendError("NOT_YOUR_GREEN_CHOICE");
      return;
    }
    if (msg.payload.cardId !== pendingGreen.cardId) {
      sendError("GREEN_CHOICE_CARD_MISMATCH");
      return;
    }

    const partnerUserId =
      typeof msg.payload.partnerUserId === "string" ? msg.payload.partnerUserId : undefined;
    const primaryResult = applyCardAndCollectDeltas(
      game,
      room.players,
      userId,
      pendingGreen.card,
      pendingGreen.deckKey,
      partnerUserId
    );
    clearPendingGreenChoice(game);

    const affectedPlayerIds = Array.from(primaryResult.deltasByUser.keys());
    if (!affectedPlayerIds.includes(userId)) affectedPlayerIds.push(userId);
    const playerDeckState = getPlayerDeckState(game, userId);

    game.history.push({
      type: "card",
      playerId: userId,
      resolvedCardId: pendingGreen.card.id,
      deckKey: pendingGreen.deckKey,
      deltas: primaryResult.deltas,
      affectedPlayers: affectedPlayerIds,
      chainedCards: [],
      grants: playerDeckState.grants,
      at: new Date().toISOString(),
    });

    const winnerUserId = pickWinnerAtSuccess12(room.players, game);
    if (winnerUserId) {
      setPendingCardWin(game, {
        winnerUserId,
        ownerUserId: userId,
        cardId: pendingGreen.card.id,
      });
      await saveGameState(roomId, game);
      broadcast(roomId, {
        type: "game.card",
        payload: {
          playerId: userId,
          cardId: pendingGreen.card.id,
          cardType: pendingGreen.card.cardType,
          imageGuid: pendingGreen.card.imageGuid,
          deckKey: pendingGreen.deckKey,
          deltas: primaryResult.deltas,
          chainedCards: [],
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
    game.activePlayerId = next;
    game.lastDice = null;
    game.phase = "WAITING_ROLL";

    await saveGameState(roomId, game);
    broadcast(roomId, {
      type: "game.card",
      payload: {
        playerId: userId,
        cardId: pendingGreen.card.id,
        cardType: pendingGreen.card.cardType,
        imageGuid: pendingGreen.card.imageGuid,
        deckKey: pendingGreen.deckKey,
        deltas: primaryResult.deltas,
        chainedCards: [],
        checks: primaryResult.checks,
        affectedPlayers: buildAffectedPlayersSnapshot(game, affectedPlayerIds),
        grants: playerDeckState.grants,
        scores: game.scores[userId],
        money: game.money[userId],
        experience: game.experience[userId],
        playerState: buildPlayerStateSnapshot(game, userId),
      },
    } as any);
    broadcast(roomId, {
      type: "game.turn_changed",
      payload: { activePlayerId: next },
    } as any);
    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }

  const pendingCardWin = getPendingCardWin(game);
  if (pendingCardWin) {
    sendError("GAME_FINISH_PENDING_CARD_CLOSE");
    return;
  }

  const pendingGreenChoice = getPendingGreenChoice(game);
  if (pendingGreenChoice) {
    sendError("GREEN_CHOICE_REQUIRED");
    return;
  }

  if (msg.type === "game.dev_adjust_stats") {
    const targetUserId =
      typeof msg.payload.targetUserId === "string" && msg.payload.targetUserId.trim().length > 0
        ? msg.payload.targetUserId
        : userId;
    const targetInRoom = room.players.some((p) => p.userId === targetUserId);
    if (!targetInRoom) {
      sendError("TARGET_PLAYER_NOT_IN_ROOM");
      return;
    }

    ensurePlayerState(game, targetUserId);
    const deckState = getPlayerDeckState(game, targetUserId);
    const mode = msg.payload.mode === "set" ? "set" : "delta";
    const deltas = msg.payload.deltas ?? {};
    const toInt = (value: unknown) => {
      const n = Number(value);
      return Number.isFinite(n) ? Math.trunc(n) : 0;
    };

    const statKeys: Array<keyof typeof deltas> = [
      "money",
      "experience",
      "success",
      "volounteer",
      "science",
      "art",
      "media",
      "business",
      "sport",
      "tourism",
      "it",
    ];
    const applied: Record<string, number> = {};
    for (const key of statKeys) {
      const raw = toInt((deltas as any)[key]);
      if (mode === "delta") {
        if (!raw) continue;
        const appliedDelta = applyStatDelta(game, targetUserId, key, raw);
        if (appliedDelta === 0) continue;
        applied[key] = appliedDelta;
      } else {
        const appliedDelta = setStatAbsolute(game, targetUserId, key, raw);
        if (appliedDelta === 0) continue;
        applied[key] = appliedDelta;
      }
    }

    const grantsDelta = toInt(deltas.grants);
    if (mode === "delta") {
      if (grantsDelta !== 0) {
        const prev = Math.max(0, Number(deckState.grants ?? 0));
        const next = Math.max(0, prev + grantsDelta);
        deckState.grants = next;
        if (next !== prev) applied.grants = next - prev;
      }
    } else {
      const prev = Math.max(0, Number(deckState.grants ?? 0));
      const next = Math.max(0, grantsDelta);
      deckState.grants = next;
      if (next !== prev) applied.grants = next - prev;
    }

    const milestoneBonus = applySphereMilestonesForUser(game, targetUserId);
    if (milestoneBonus !== 0) {
      applied.success = (applied.success ?? 0) + milestoneBonus;
    }

    game.history.push({
      type: "dev_adjust_stats",
      by: userId,
      targetUserId,
      mode,
      deltas: applied,
      at: new Date().toISOString(),
    });

    const winnerUserId = pickWinnerAtSuccess12(room.players, game);
    if (winnerUserId) {
      await finalizeAndBroadcastWin(roomId, winnerUserId, game, broadcast);
      return;
    }

    await saveGameState(roomId, game);
    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }

  if (game.activePlayerId !== userId) {
    sendError("NOT_YOUR_TURN");
    return;
  }

  ensurePlayerState(game, userId);

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

    const card = drawRandomCard(deckKey);
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
      setPendingGreenChoice(game, {
        ownerUserId: userId,
        cardId: card.id,
        deckKey,
        card,
        candidateUserIds,
      });

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
      // Grant node rule: exp check on server, grant token on success (+1 success).
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

    // Effect 4 in Unity = DrawNextCardFromSameSphere.
    // Resolve a single extra draw to stay deterministic on server.
    const hasChainDraw = card.effects.some((e) => e.effect === 4);
    if (hasChainDraw) {
      const bonus = drawRandomCard(deckKey);
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
    game.activePlayerId = next;
    game.lastDice = null;
    game.phase = "WAITING_ROLL";

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

    broadcast(roomId, {
      type: "game.turn_changed",
      payload: { activePlayerId: next },
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
}