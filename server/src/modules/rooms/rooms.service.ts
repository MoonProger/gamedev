import { prisma } from "../../db/prisma";
import { RoomStatus } from "@prisma/client";

export async function createRoom(creatorId: string, title: string, settings: any) {
  // Создатель автоматически добавляется игроком в комнату
  return prisma.room.create({
    data: {
      title,
      creatorId,
      status: "WAITING",
      settings: JSON.stringify(settings || { maxPlayers: 5 }),
      players: { create: { userId: creatorId } },
    },
    include: {
      players: { include: { user: { select: { id: true, username: true } } } },
      creator: { select: { id: true, username: true } },
    },
  });
}

export async function listRooms() {
  return prisma.room.findMany({
    where: { status: "WAITING" },
    orderBy: { createdAt: "desc" },
    include: {
      players: { include: { user: { select: { id: true, username: true } } } },
      creator: { select: { id: true, username: true } },
    },
  });
}

export async function getRoom(roomId: string) {
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

  const count = await prisma.roomPlayer.count({ where: { roomId } });
  const maxPlayers = (room.settings as any)?.maxPlayers ?? 5;
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
