import bcrypt from "bcrypt";
import { prisma } from "../../db/prisma";

export async function createRoom(
  creatorId: string,
  title: string,
  settings: any,
  password?: string
) {
  const hashedPassword = password ? await bcrypt.hash(password, 10) : null;

  return prisma.room.create({
    data: {
      title,
      creatorId,
      status: "WAITING",
      settings: JSON.stringify(settings ?? {}),
      password: hashedPassword,
      players: { create: { userId: creatorId } },
    },
    include: {
      players: { include: { user: { select: { id: true, username: true } } } },
      creator: { select: { id: true, username: true } },
    },
  });
}

export async function listRooms() {
  const rooms = await prisma.room.findMany({
    where: { status: "WAITING" },
    orderBy: { createdAt: "desc" },
    include: {
      players: { include: { user: { select: { id: true, username: true } } } },
      creator: { select: { id: true, username: true } },
    },
  });

  return rooms.map((room) => ({
    ...room,
    settings: safeJsonParse(room.settings, {}),
    password: room.password ? "***" : null,
  }));
}

export async function getRoom(roomId: string) {
  const room = await prisma.room.findUnique({
    where: { id: roomId },
    include: {
      players: { include: { user: { select: { id: true, username: true } } } },
      creator: { select: { id: true, username: true } },
    },
  });

  if (!room) return null;

  return {
    ...room,
    settings: safeJsonParse(room.settings, {}),
    password: room.password ? "***" : null,
  };
}

export async function getRawRoom(roomId: string) {
  return prisma.room.findUnique({
    where: { id: roomId },
    include: {
      players: { include: { user: { select: { id: true, username: true } } } },
      creator: { select: { id: true, username: true } },
    },
  });
}

export async function joinRoom(roomId: string, userId: string) {
  const room = await prisma.room.findUnique({ where: { id: roomId } });
  if (!room) throw new Error("ROOM_NOT_FOUND");
  if (room.status !== "WAITING") throw new Error("ROOM_NOT_JOINABLE");
  if (room.password) throw new Error("ROOM_PASSWORD_REQUIRED");

  const settings = safeJsonParse(room.settings, {});
  const count = await prisma.roomPlayer.count({ where: { roomId } });
  const maxPlayers = settings?.maxPlayers ?? 5;
  if (count >= maxPlayers) throw new Error("ROOM_FULL");

  await prisma.roomPlayer.upsert({
    where: { roomId_userId: { roomId, userId } },
    create: { roomId, userId },
    update: {},
  });

  return getRoom(roomId);
}

export async function joinPrivateRoom(roomId: string, userId: string, password: string) {
  const room = await prisma.room.findUnique({ where: { id: roomId } });
  if (!room) throw new Error("ROOM_NOT_FOUND");
  if (room.status !== "WAITING") throw new Error("ROOM_NOT_JOINABLE");
  if (!room.password) throw new Error("ROOM_HAS_NO_PASSWORD");

  const ok = await bcrypt.compare(password, room.password);
  if (!ok) throw new Error("INVALID_ROOM_PASSWORD");

  const settings = safeJsonParse(room.settings, {});
  const count = await prisma.roomPlayer.count({ where: { roomId } });
  const maxPlayers = settings?.maxPlayers ?? 5;
  if (count >= maxPlayers) throw new Error("ROOM_FULL");

  await prisma.roomPlayer.upsert({
    where: { roomId_userId: { roomId, userId } },
    create: { roomId, userId },
    update: {},
  });

  return getRoom(roomId);
}

export async function leaveRoom(roomId: string, userId: string) {
  await prisma.roomPlayer.deleteMany({ where: { roomId, userId } });
  return getRoom(roomId);
}

export async function setReady(roomId: string, userId: string, ready: boolean) {
  await prisma.roomPlayer.update({
    where: { roomId_userId: { roomId, userId } },
    data: { isReady: ready },
  });
  return getRoom(roomId);
}

function safeJsonParse(value: string | null, fallback: any) {
  try {
    return value ? JSON.parse(value) : fallback;
  } catch {
    return fallback;
  }
}