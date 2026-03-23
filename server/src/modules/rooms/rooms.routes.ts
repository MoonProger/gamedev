import { Router } from "express";
import { authRequired } from "../../middlewares/auth";
import {
  CreateRoomSchema,
  ReadySchema,
  JoinPrivateRoomSchema,
} from "./rooms.schemas";
import {
  createRoom,
  listRooms,
  getRoom,
  joinRoom,
  joinPrivateRoom,
  leaveRoom,
  setReady,
} from "./rooms.service";
import { prisma } from "../../db/prisma";

export const roomsRoutes = Router();

function getRoomId(req: any): string {
  const id = req.params.id;
  if (Array.isArray(id)) return id[0];
  return id;
}

roomsRoutes.get("/", async (_req, res) => {
  const rooms = await listRooms();
  res.json({ rooms });
});

roomsRoutes.get("/:id", async (req, res) => {
  const roomId = getRoomId(req);
  const room = await getRoom(roomId);
  if (!room) return res.status(404).json({ error: "Room not found" });
  res.json({ room });
});

roomsRoutes.post("/", authRequired, async (req, res) => {
  const parsed = CreateRoomSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: parsed.error.flatten() });

  const room = await createRoom(
    req.auth!.userId,
    parsed.data.title,
    parsed.data.settings,
    parsed.data.password
  );

  res.json({ room });
});

roomsRoutes.post("/:id/join", authRequired, async (req, res) => {
  const roomId = getRoomId(req);
  try {
    const room = await joinRoom(roomId, req.auth!.userId);
    res.json({ room });
  } catch (e: any) {
    if (e.message === "ROOM_NOT_FOUND") return res.status(404).json({ error: "Room not found" });
    if (e.message === "ROOM_FULL") return res.status(409).json({ error: "Room full" });
    if (e.message === "ROOM_NOT_JOINABLE") return res.status(409).json({ error: "Room not joinable" });
    if (e.message === "ROOM_PASSWORD_REQUIRED") return res.status(403).json({ error: "Password required" });
    return res.status(500).json({ error: "Server error" });
  }
});

roomsRoutes.post("/:id/join-private", authRequired, async (req, res) => {
  const parsed = JoinPrivateRoomSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: parsed.error.flatten() });

  const roomId = getRoomId(req);
  try {
    const room = await joinPrivateRoom(roomId, req.auth!.userId, parsed.data.password);
    res.json({ room });
  } catch (e: any) {
    if (e.message === "ROOM_NOT_FOUND") return res.status(404).json({ error: "Room not found" });
    if (e.message === "ROOM_FULL") return res.status(409).json({ error: "Room full" });
    if (e.message === "ROOM_NOT_JOINABLE") return res.status(409).json({ error: "Room not joinable" });
    if (e.message === "ROOM_HAS_NO_PASSWORD") return res.status(409).json({ error: "Room has no password" });
    if (e.message === "INVALID_ROOM_PASSWORD") return res.status(403).json({ error: "Invalid room password" });
    return res.status(500).json({ error: "Server error" });
  }
});

roomsRoutes.post("/:id/leave", authRequired, async (req, res) => {
  const roomId = getRoomId(req);
  const room = await leaveRoom(roomId, req.auth!.userId);
  res.json({ room });
});

roomsRoutes.post("/:id/ready", authRequired, async (req, res) => {
  const parsed = ReadySchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: parsed.error.flatten() });

  const roomId = getRoomId(req);
  try {
    const room = await setReady(roomId, req.auth!.userId, parsed.data.ready);
    res.json({ room });
  } catch {
    return res.status(409).json({ error: "Not in room" });
  }
});

roomsRoutes.delete("/:id", authRequired, async (req, res) => {
  const roomId = getRoomId(req);
  try {
    const room = await prisma.room.findUnique({
      where: { id: roomId },
      include: { players: true }
    });
    
    if (!room) {
      return res.status(404).json({ error: "Room not found" });
    }
    
    if (room.creatorId !== req.auth!.userId) {
      return res.status(403).json({ error: "Only creator can delete room" });
    }
    
    await prisma.room.delete({
      where: { id: roomId }
    });
    
    res.json({ success: true });
  } catch (error) {
    res.status(500).json({ error: "Failed to delete room" });
  }
});

roomsRoutes.post("/:id/close", authRequired, async (req, res) => {
  const roomId = getRoomId(req);
  try {
    const room = await prisma.room.findUnique({
      where: { id: roomId }
    });
    
    if (!room) {
      return res.status(404).json({ error: "Room not found" });
    }
    
    if (room.creatorId !== req.auth!.userId) {
      return res.status(403).json({ error: "Only creator can close room" });
    }
    
    const updated = await prisma.room.update({
      where: { id: roomId },
      data: { status: "CLOSED" }
    });
    
    res.json({ room: updated });
  } catch (error) {
    res.status(500).json({ error: "Failed to close room" });
  }
});

roomsRoutes.post("/:id/open", authRequired, async (req, res) => {
  const roomId = getRoomId(req);
  try {
    const room = await prisma.room.findUnique({
      where: { id: roomId }
    });
    
    if (!room) {
      return res.status(404).json({ error: "Room not found" });
    }
    
    if (room.creatorId !== req.auth!.userId) {
      return res.status(403).json({ error: "Only creator can open room" });
    }
    
    const updated = await prisma.room.update({
      where: { id: roomId },
      data: { status: "WAITING" }
    });
    
    res.json({ room: updated });
  } catch (error) {
    res.status(500).json({ error: "Failed to open room" });
  }
});