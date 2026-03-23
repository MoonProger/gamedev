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

export const roomsRoutes = Router();

roomsRoutes.get("/", async (_req, res) => {
  const rooms = await listRooms();
  res.json({ rooms });
});

roomsRoutes.get("/:id", async (req, res) => {
  const room = await getRoom(req.params.id);
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
  try {
    const room = await joinRoom(req.params.id, req.auth!.userId);
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

  try {
    const room = await joinPrivateRoom(req.params.id, req.auth!.userId, parsed.data.password);
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
  const room = await leaveRoom(req.params.id, req.auth!.userId);
  res.json({ room });
});

roomsRoutes.post("/:id/ready", authRequired, async (req, res) => {
  const parsed = ReadySchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: parsed.error.flatten() });

  try {
    const room = await setReady(req.params.id, req.auth!.userId, parsed.data.ready);
    res.json({ room });
  } catch {
    return res.status(409).json({ error: "Not in room" });
  }
});