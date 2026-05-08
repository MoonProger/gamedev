import bcrypt from "bcrypt";
import { prisma } from "../../db/prisma";
import { resetGame } from "../game/game.state";

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

export async function listRoomsPaginated(params: {
  page?: number;
  limit?: number;
  search?: string;
  status?: string;
  hasPassword?: boolean;
}) {
  const page = Math.max(1, Number(params.page ?? 1));
  const limit = Math.min(50, Math.max(1, Number(params.limit ?? 10)));
  const skip = (page - 1) * limit;

  const where: any = {};

  if (params.status) {
    where.status = params.status;
  }

  if (params.search) {
    where.title = {
      contains: params.search,
    };
  }

  if (params.hasPassword === true) {
    where.password = {
      not: null,
    };
  }

  if (params.hasPassword === false) {
    where.password = null;
  }

  const [rooms, total] = await Promise.all([
    prisma.room.findMany({
      where,
      skip,
      take: limit,
      orderBy: {
        createdAt: "desc",
      },
      include: {
        players: {
          include: {
            user: {
              select: {
                id: true,
                username: true,
              },
            },
          },
        },
        creator: {
          select: {
            id: true,
            username: true,
          },
        },
      },
    }),
    prisma.room.count({ where }),
  ]);

  return {
    rooms: rooms.map((room) => ({
      id: room.id,
      title: room.title,
      status: room.status,
      settings: safeJsonParse(room.settings, {}),
      hasPassword: Boolean(room.password),
      createdAt: room.createdAt,
      creator: room.creator,
      playersCount: room.players.length,
      players: room.players.map((player) => ({
        userId: player.userId,
        username: player.user?.username ?? null,
        isReady: player.isReady,
        joinedAt: player.joinedAt,
      })),
    })),
    pagination: {
      page,
      limit,
      total,
      totalPages: Math.ceil(total / limit),
    },
  };
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
  const maxPlayers = settings?.maxPlayers ?? 4;
  
  // Проверяем, есть ли игрок уже в комнате
  const existingPlayer = await prisma.roomPlayer.findUnique({
    where: { roomId_userId: { roomId, userId } }
  });
  
  // Если игрок уже в комнате, просто возвращаем комнату без ошибки
  if (existingPlayer) {
    console.log(`User ${userId} already in room ${roomId}, skipping join`);
    return getRoom(roomId);
  }
  
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
  const maxPlayers = settings?.maxPlayers ?? 4;
  
  // Проверяем, есть ли игрок уже в комнате
  const existingPlayer = await prisma.roomPlayer.findUnique({
    where: { roomId_userId: { roomId, userId } }
  });
  
  // Если игрок уже в комнате, просто возвращаем комнату без ошибки
  if (existingPlayer) {
    console.log(`User ${userId} already in room ${roomId}, skipping join`);
    return getRoom(roomId);
  }
  
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

export async function deleteRoom(roomId: string, userId: string) {
  const room = await prisma.room.findUnique({ where: { id: roomId } });
  if (!room) throw new Error("ROOM_NOT_FOUND");
  if (room.creatorId !== userId) throw new Error("NOT_AUTHORIZED");
  
  await prisma.room.delete({ where: { id: roomId } });
  return { success: true };
}

export async function closeRoom(roomId: string, userId: string) {
  const room = await prisma.room.findUnique({ where: { id: roomId } });
  if (!room) throw new Error("ROOM_NOT_FOUND");
  if (room.creatorId !== userId) throw new Error("NOT_AUTHORIZED");
  
  return prisma.room.update({
    where: { id: roomId },
    data: { status: "CLOSED" }
  });
}

export async function openRoom(roomId: string, userId: string) {
  const room = await prisma.room.findUnique({ where: { id: roomId } });
  if (!room) throw new Error("ROOM_NOT_FOUND");
  if (room.creatorId !== userId) throw new Error("NOT_AUTHORIZED");
  
  return prisma.room.update({
    where: { id: roomId },
    data: { status: "WAITING" }
  });
}
export async function deleteRoomBySystem(roomId: string) {
  const room = await prisma.room.findUnique({
    where: { id: roomId },
    select: { id: true },
  });
  if (!room) return false;

  await prisma.room.delete({
    where: { id: roomId },
  });
  return true;
}

export async function resetGameState(roomId: string) {
  console.log(`Resetting game state for room ${roomId}`);
  
  // Очищаем in-memory состояние игры
  resetGame(roomId);
  
  // Удаляем сохранённое состояние из БД
  await prisma.gameState.delete({
    where: { roomId }
  }).catch(() => {});
  
  // Сбрасываем статус комнаты на WAITING
  await prisma.room.update({
    where: { id: roomId },
    data: { status: "WAITING" }
  }).catch(() => {});
  
  // Сбрасываем готовность всех игроков
  await prisma.roomPlayer.updateMany({
    where: { roomId },
    data: { isReady: false }
  }).catch(() => {});
}