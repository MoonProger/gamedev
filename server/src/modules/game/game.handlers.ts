import { WsIn, WsOut } from "../ws/ws.types";
import { prisma } from "../../db/prisma";
import { getOrCreateGame } from "./game.state";
import { saveGameState } from "./game.persistence";

function randDice() {
  return 1 + Math.floor(Math.random() * 6);
}

function ensurePlayerState(game: any, userId: string) {
  if (game.positions[userId] === undefined) game.positions[userId] = 0;
  if (!game.scores[userId]) game.scores[userId] = {};
  if (game.money[userId] === undefined) game.money[userId] = 0;
  if (game.experience[userId] === undefined) game.experience[userId] = 0;
}

function nextTurn(roomPlayers: { userId: string }[], currentUserId: string) {
  const idx = roomPlayers.findIndex((p) => p.userId === currentUserId);
  return roomPlayers[(idx + 1) % roomPlayers.length].userId;
}

export async function handleGameMessage(ctx: {
  roomId: string;
  userId: string;
  msg: WsIn;
  broadcast: (roomId: string, msg: WsOut) => void;
}) {
  const { roomId, userId, msg, broadcast } = ctx;

  const room = await prisma.room.findUnique({
    where: { id: roomId },
    include: { players: true },
  });

  if (!room) {
    broadcast(roomId, { type: "error", payload: { message: "ROOM_NOT_FOUND" } });
    return;
  }

  const game = getOrCreateGame(roomId);

  if (msg.type === "game.start") {
    if (game.started) {
      broadcast(roomId, { type: "error", payload: { message: "GAME_ALREADY_STARTED" } });
      return;
    }

    const players = room.players;
    const allReady = players.length >= 1 && players.every((p) => p.isReady);

    if (!allReady) {
      broadcast(roomId, { type: "error", payload: { message: "NOT_ALL_READY_OR_TOO_FEW_PLAYERS" } });
      return;
    }

    game.started = true;
    game.isPaused = false;
    game.activePlayerId = players[0].userId;
    game.lastDice = null;
    game.phase = "WAITING_ROLL";

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
    broadcast(roomId, { type: "error", payload: { message: "GAME_NOT_STARTED" } });
    return;
  }

  if (game.isPaused) {
    broadcast(roomId, { type: "error", payload: { message: "GAME_PAUSED" } });
    return;
  }

  if (game.activePlayerId !== userId) {
    broadcast(roomId, { type: "error", payload: { message: "NOT_YOUR_TURN" } });
    return;
  }

  ensurePlayerState(game, userId);

  if (msg.type === "game.roll_dice") {
    if (game.phase !== "WAITING_ROLL") {
      broadcast(roomId, { type: "error", payload: { message: "WRONG_PHASE" } });
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
      broadcast(roomId, { type: "error", payload: { message: "ROLL_DICE_FIRST" } });
      return;
    }

    const steps = Number(msg.payload.steps);
    if (!Number.isFinite(steps) || steps < 1 || steps > 6) {
      broadcast(roomId, { type: "error", payload: { message: "INVALID_STEPS" } });
      return;
    }

    if (steps !== game.lastDice) {
      broadcast(roomId, { type: "error", payload: { message: "STEPS_MUST_EQUAL_DICE" } });
      return;
    }

    const fromSector = game.positions[userId] ?? 0;
    const toSector = fromSector + steps;

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

    await saveGameState(roomId, game);

    broadcast(roomId, {
      type: "game.move",
      payload: {
        playerId: userId,
        fromSector,
        toSector,
        dice: game.lastDice,
      },
    } as any);

    broadcast(roomId, {
      type: "game.token_moved",
      payload: { playerId: userId, pos: toSector, steps },
    } as any);

    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }

  if (msg.type === "game.card") {
    if (game.phase !== "WAITING_ACTION") {
      broadcast(roomId, { type: "error", payload: { message: "WRONG_PHASE_FOR_CARD" } });
      return;
    }

    const effects = msg.payload.effects ?? {};
    const sphereScores = effects.sphereScores ?? {};

    game.money[userId] = (game.money[userId] ?? 0) + (effects.money ?? 0);
    game.experience[userId] = (game.experience[userId] ?? 0) + (effects.experience ?? 0);

    for (const [sphere, delta] of Object.entries(sphereScores)) {
      game.scores[userId][sphere] = (game.scores[userId][sphere] ?? 0) + Number(delta);
    }

    if (effects.successPoints) {
      game.scores[userId]["success"] = (game.scores[userId]["success"] ?? 0) + effects.successPoints;
    }

    game.history.push({
      type: "card",
      playerId: userId,
      cardId: msg.payload.cardId,
      effects,
      at: new Date().toISOString(),
    });

    const next = nextTurn(room.players, userId);
    game.activePlayerId = next;
    game.lastDice = null;
    game.phase = "WAITING_ROLL";

    await saveGameState(roomId, game);

    broadcast(roomId, {
      type: "game.card",
      payload: {
        playerId: userId,
        cardId: msg.payload.cardId,
        effects,
        scores: game.scores[userId],
        money: game.money[userId],
        experience: game.experience[userId],
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
      broadcast(roomId, { type: "error", payload: { message: "WRONG_PHASE_FOR_PROJECT" } });
      return;
    }

    const successPoints = Number(msg.payload.successPoints);
    if (!Number.isFinite(successPoints) || successPoints < 0) {
      broadcast(roomId, { type: "error", payload: { message: "INVALID_SUCCESS_POINTS" } });
      return;
    }

    game.scores[userId]["success"] = (game.scores[userId]["success"] ?? 0) + successPoints;

    game.history.push({
      type: "project",
      playerId: userId,
      projectId: msg.payload.projectId,
      successPoints,
      at: new Date().toISOString(),
    });

    const next = nextTurn(room.players, userId);
    game.activePlayerId = next;
    game.lastDice = null;
    game.phase = "WAITING_ROLL";

    await saveGameState(roomId, game);

    broadcast(roomId, {
      type: "game.project",
      payload: {
        playerId: userId,
        projectId: msg.payload.projectId,
        successPoints,
        totalSuccess: game.scores[userId]["success"],
      },
    } as any);

    broadcast(roomId, {
      type: "game.turn_changed",
      payload: { activePlayerId: next },
    } as any);

    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }
}