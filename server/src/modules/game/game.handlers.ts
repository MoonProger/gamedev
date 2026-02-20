import { WsIn, WsOut } from "../ws/ws.types";
import { prisma } from "../../db/prisma";
import { getOrCreateGame } from "./game.state";

function randDice() {
  return 1 + Math.floor(Math.random() * 6);
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

  // game.start
  if (msg.type === "game.start") {
    const players = room.players;
    const allReady = players.length >= 1 && players.every((p) => p.isReady); // для теста можно 1 игрока, в реальной игре должно быть 2 и более
    if (!allReady) {
      broadcast(roomId, { type: "error", payload: { message: "NOT_ALL_READY_OR_TOO_FEW_PLAYERS" } });
      return;
    }

    game.started = true;
    game.activePlayerId = players[0].userId;
    game.lastDice = null;
    game.phase = "WAITING_ROLL";

    for (const p of players) {
      if (game.positions[p.userId] === undefined) game.positions[p.userId] = 0;
    }

    broadcast(roomId, { type: "game.started", payload: { activePlayerId: game.activePlayerId } } as any);
    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }

  if (!game.started) {
    broadcast(roomId, { type: "error", payload: { message: "GAME_NOT_STARTED" } });
    return;
  }

  // только активный игрок
  if (game.activePlayerId !== userId) {
    broadcast(roomId, { type: "error", payload: { message: "NOT_YOUR_TURN" } });
    return;
  }

  // game.roll_dice
  if (msg.type === "game.roll_dice") {
    if (game.phase !== "WAITING_ROLL") {
      broadcast(roomId, { type: "error", payload: { message: "WRONG_PHASE" } });
      return;
    }

    const value = randDice();
    game.lastDice = value;
    game.phase = "WAITING_MOVE";

    broadcast(roomId, { type: "game.dice_rolled", payload: { value } } as any);
    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }

  // game.move
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

    // разрешаем двигаться только на то число, что выпало
    if (steps !== game.lastDice) {
      broadcast(roomId, { type: "error", payload: { message: "STEPS_MUST_EQUAL_DICE" } });
      return;
    }

    game.positions[userId] = (game.positions[userId] ?? 0) + steps;

    broadcast(roomId, {
      type: "game.token_moved",
      payload: { playerId: userId, pos: game.positions[userId], steps },
    } as any);

    // смена хода
    const players = room.players;
    const idx = players.findIndex((p) => p.userId === userId);
    const next = players[(idx + 1) % players.length].userId;

    game.activePlayerId = next;
    game.lastDice = null;
    game.phase = "WAITING_ROLL";

    broadcast(roomId, { type: "game.turn_changed", payload: { activePlayerId: next } } as any);
    broadcast(roomId, { type: "game.state", payload: game } as any);
    return;
  }
}
