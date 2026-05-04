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

  addSocketToRoom(roomId, ws, meta);

  broadcast(roomId, {
    type: "room.player_joined",
    payload: { userId: meta.userId },
  });

  await pushRoomState(roomId);

  const persistedGame = await loadGameState(roomId);

  if (!persistedGame) return;

  const restored = {
    ...persistedGame,
  };

  if (restored.started) {
    const allOnline = await areAllRoomPlayersOnline(roomId);

    restored.isPaused = !allOnline;

    setGame(roomId, restored as any);
    await saveGameState(roomId, restored as any);

    if (allOnline) {
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

  removeSocketFromRoom(ws, roomId);

  broadcast(roomId, {
    type: "room.player_left",
    payload: { userId: meta.userId },
  });

  const game = getOrCreateGame(roomId);

  if (game.started) {
    game.isPaused = true;

    await saveGameState(roomId, game as any);

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
        await setReady(meta.roomId, meta.userId, msg.payload.ready);
        await pushRoomState(meta.roomId);
        return;
      }

      await handleGameMessage({
        roomId: meta.roomId,
        userId: meta.userId,
        msg,
        broadcast,
      });
    });

    ws.on("close", async () => {
      await handleDisconnect(ws);
    });

    ws.on("error", (error) => {
      console.error("[ws:error]", error);
    });
  });
}
