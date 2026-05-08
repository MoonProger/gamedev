import { Server as HttpServer } from "http";
import { WebSocketServer, WebSocket } from "ws";
import jwt from "jsonwebtoken";

import { WsIn, WsOut } from "./ws.types";
import { handleGameMessage } from "../game/game.handlers";
import { roomToDto } from "../rooms/rooms.dto";
import {
  joinRoom,
  joinPrivateRoom,
  leaveRoom,
  setReady,
  getRoom,
  getRawRoom,
  resetGameState,
  deleteRoomBySystem,
} from "../rooms/rooms.service";
import { loadGameState, saveGameState } from "../game/game.persistence";
import { getOrCreateGame, setGame } from "../game/game.state";

type JwtPayload = {
  userId: string;
  email: string;
};

type ClientMeta = {
  userId: string;
  roomId?: string;
};

const clients = new Map<WebSocket, ClientMeta>();
const roomSockets = new Map<string, Set<WebSocket>>();
const disconnectTimers = new Map<WebSocket, NodeJS.Timeout>();
const replacedSockets = new Set<WebSocket>();
const roomLastActivityAt = new Map<string, number>();

const ROOM_IDLE_TIMEOUT_MS = 10 * 60 * 1000;
const ROOM_CLEANUP_INTERVAL_MS = 60 * 1000;

function safeSend(ws: WebSocket, msg: WsOut) {
  if (ws.readyState === ws.OPEN) {
    ws.send(JSON.stringify(msg));
  }
}

function broadcast(roomId: string, msg: WsOut) {
  const set = roomSockets.get(roomId);

  console.log(
    `[ws:broadcast] room=${roomId}, type=${msg.type}, sockets=${set?.size ?? 0}`
  );

  if (!set) return;

  for (const ws of set) {
    safeSend(ws, msg);
  }
}

async function pushRoomState(roomId: string) {
  const room = await getRoom(roomId);
  if (!room) return;

  broadcast(roomId, {
    type: "room.state",
    payload: roomToDto(room),
  });
}

function extractToken(reqUrl?: string): string | null {
  if (!reqUrl) return null;

  const u = new URL(reqUrl, "http://localhost");
  return u.searchParams.get("token");
}

function addSocketToRoom(roomId: string, ws: WebSocket, meta: ClientMeta) {
  if (meta.roomId && roomSockets.has(meta.roomId)) {
    roomSockets.get(meta.roomId)!.delete(ws);
  }

  meta.roomId = roomId;
  clients.set(ws, meta);

  if (!roomSockets.has(roomId)) {
    roomSockets.set(roomId, new Set());
  }

  roomSockets.get(roomId)!.add(ws);
}

function touchRoom(roomId?: string) {
  if (!roomId) return;
  roomLastActivityAt.set(roomId, Date.now());
}

function evictDuplicateUserSockets(roomId: string, userId: string, keepWs: WebSocket) {
  const set = roomSockets.get(roomId);
  if (!set) return;

  for (const otherWs of Array.from(set)) {
    if (otherWs === keepWs) continue;
    const otherMeta = clients.get(otherWs);
    if (!otherMeta || otherMeta.userId !== userId) continue;

    removeSocketFromRoom(otherWs, roomId);
    clients.delete(otherWs);
    const timer = disconnectTimers.get(otherWs);
    if (timer) {
      clearTimeout(timer);
      disconnectTimers.delete(otherWs);
    }
    replacedSockets.add(otherWs);
    try {
      otherWs.close(1000, "Replaced by newer connection");
    } catch {
      // ignore close race
    }
  }
}

function removeSocketFromRoom(ws: WebSocket, roomId: string) {
  const set = roomSockets.get(roomId);

  if (!set) return;

  set.delete(ws);

  if (set.size === 0) {
    roomSockets.delete(roomId);
  }
}

function getOnlineUserIds(roomId: string) {
  const set = roomSockets.get(roomId);
  const userIds = new Set<string>();

  if (!set) return userIds;

  for (const ws of set) {
    const meta = clients.get(ws);

    if (meta?.userId && ws.readyState === ws.OPEN) {
      userIds.add(meta.userId);
    }
  }

  return userIds;
}

async function areAllRoomPlayersOnline(roomId: string) {
  const room = await getRawRoom(roomId);
  if (!room) return false;

  const onlineUserIds = getOnlineUserIds(roomId);

  return room.players.every((player) => onlineUserIds.has(player.userId));
}

async function handleRoomJoin(ws: WebSocket, meta: ClientMeta, msg: Extract<WsIn, { type: "room.join" }>) {
  const roomId = msg.payload.roomId;
  const password = msg.payload.password;
  const alreadyInSameRoom = meta.roomId === roomId && roomSockets.get(roomId)?.has(ws);

  if (!alreadyInSameRoom) {
    try {
      if (password) {
        await joinPrivateRoom(roomId, meta.userId, password);
      } else {
        await joinRoom(roomId, meta.userId);
      }
    } catch (e: any) {
      safeSend(ws, {
        type: "error",
        payload: { message: e.message },
      });
      return;
    }
  }

  addSocketToRoom(roomId, ws, meta);
  evictDuplicateUserSockets(roomId, meta.userId, ws);
  touchRoom(roomId);

  if (!alreadyInSameRoom) {
    broadcast(roomId, {
      type: "room.player_joined",
      payload: { userId: meta.userId },
    });
  }

  await pushRoomState(roomId);

  const persistedGame = await loadGameState(roomId);

  if (!persistedGame) return;

  const restored = {
    ...persistedGame,
  };

  if (restored.started) {
    const allOnline = await areAllRoomPlayersOnline(roomId);
    const wasPaused = Boolean(restored.isPaused);
    restored.isPaused = !allOnline;

    setGame(roomId, restored as any);
    await saveGameState(roomId, restored as any);

    if (allOnline && wasPaused) {
      broadcast(roomId, {
        type: "game.resumed",
        payload: {},
      } as any);
    }

    broadcast(roomId, {
      type: "game.state",
      payload: restored,
    } as any);

    return;
  }

  setGame(roomId, restored as any);
  safeSend(ws, {
    type: "game.state",
    payload: restored,
  } as any);
}

async function handleRoomLeave(ws: WebSocket, meta: ClientMeta) {
  if (!meta.roomId) return;

  const roomId = meta.roomId;
  touchRoom(roomId);

  await leaveRoom(roomId, meta.userId);
  removeSocketFromRoom(ws, roomId);

  meta.roomId = undefined;
  clients.set(ws, meta);

  const room = await getRawRoom(roomId);
  const playersCount = room?.players.length || 0;

  console.log(
    `[ws:leave] player=${meta.userId}, room=${roomId}, remainingPlayers=${playersCount}`
  );

  if (playersCount === 0) {
    console.log(`[ws:leave] no players left, reset game state, room=${roomId}`);
    await resetGameState(roomId);
  }

  broadcast(roomId, {
    type: "room.player_left",
    payload: { userId: meta.userId },
  });

  await pushRoomState(roomId);
}

async function handleDisconnect(ws: WebSocket) {
  const meta = clients.get(ws);

  clients.delete(ws);

  if (!meta?.roomId) return;

  const roomId = meta.roomId;
  touchRoom(roomId);

  removeSocketFromRoom(ws, roomId);

  broadcast(roomId, {
    type: "room.player_left",
    payload: { userId: meta.userId },
  });

  const room = await getRawRoom(roomId);
  if (!room) {
    roomLastActivityAt.delete(roomId);
    return;
  }

  const game = getOrCreateGame(roomId);

  if (game.started) {
    game.isPaused = true;
    try {
      await saveGameState(roomId, game as any);
    } catch (error: any) {
      console.warn(`[ws:disconnect] failed to persist game state for room=${roomId}: ${error?.message ?? error}`);
      return;
    }

    broadcast(roomId, {
      type: "game.paused",
      payload: { reason: `Player ${meta.userId} disconnected` },
    } as any);

    broadcast(roomId, {
      type: "game.state",
      payload: game,
    } as any);
  }

  await pushRoomState(roomId);
}

async function cleanupIdleRooms() {
  const now = Date.now();
  for (const [roomId, lastActivityAt] of roomLastActivityAt.entries()) {
    if (now - lastActivityAt < ROOM_IDLE_TIMEOUT_MS) continue;

    const sockets = roomSockets.get(roomId);
    if (sockets && sockets.size > 0) continue;

    const room = await getRawRoom(roomId);
    if (!room) {
      roomLastActivityAt.delete(roomId);
      continue;
    }

    try {
      await resetGameState(roomId);
      const deleted = await deleteRoomBySystem(roomId);
      if (deleted) {
        console.log(`[ws:cleanup] removed idle room=${roomId} after 10 minutes inactivity`);
      }
    } catch (error: any) {
      console.error(`[ws:cleanup] failed for room=${roomId}:`, error?.message ?? error);
    } finally {
      roomLastActivityAt.delete(roomId);
    }
  }
}

export function attachWs(server: HttpServer) {
  const wss = new WebSocketServer({
    server,
    path: "/ws",
  });

  wss.on("connection", (ws, req) => {
    const token = extractToken(req.url);
    const secret = process.env.JWT_SECRET;

    if (!token || !secret) {
      ws.close(1008, "No token/secret");
      return;
    }

    let payload: JwtPayload;

    try {
      payload = jwt.verify(token, secret) as JwtPayload;
    } catch {
      ws.close(1008, "Invalid token");
      return;
    }

    clients.set(ws, {
      userId: payload.userId,
    });

    safeSend(ws, {
      type: "connected",
      payload: { userId: payload.userId },
    });

    ws.on("message", async (raw) => {
      const pending = disconnectTimers.get(ws);
      if (pending) {
        clearTimeout(pending);
        disconnectTimers.delete(ws);
      }

      let msg: WsIn;

      try {
        msg = JSON.parse(raw.toString());
      } catch {
        safeSend(ws, {
          type: "error",
          payload: { message: "Bad JSON" },
        });
        return;
      }

      const meta = clients.get(ws);
      if (!meta) return;

      if (msg.type === "ping") {
        safeSend(ws, {
          type: "pong",
          payload: msg.payload ?? null,
        });
        return;
      }

      if (msg.type === "room.join") {
        await handleRoomJoin(ws, meta, msg);
        return;
      }

      if (!meta.roomId) {
        safeSend(ws, {
          type: "error",
          payload: { message: "Join room first" },
        });
        return;
      }

      if (msg.type === "room.leave") {
        await handleRoomLeave(ws, meta);
        return;
      }

      if (msg.type === "room.ready") {
        touchRoom(meta.roomId);
        await setReady(meta.roomId, meta.userId, msg.payload.ready);
        await pushRoomState(meta.roomId);
        return;
      }

      touchRoom(meta.roomId);
      await handleGameMessage({
        roomId: meta.roomId,
        userId: meta.userId,
        msg,
        broadcast,
        reply: (outMsg) => safeSend(ws, outMsg),
      });
    });

    ws.on("close", async () => {
      if (replacedSockets.has(ws)) {
        replacedSockets.delete(ws);
        return;
      }
      const timer = setTimeout(async () => {
        disconnectTimers.delete(ws);
        try {
          await handleDisconnect(ws);
        } catch (error: any) {
          console.error("[ws:disconnect] failed:", error?.message ?? error);
        }
      }, 4000);
      disconnectTimers.set(ws, timer);
    });

    ws.on("error", (error) => {
      console.error("[ws:error]", error);
    });
  });

  setInterval(() => {
    void cleanupIdleRooms();
  }, ROOM_CLEANUP_INTERVAL_MS);
}
