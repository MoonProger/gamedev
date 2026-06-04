export function roomToDto(room: any) {
  return {
    id: room.id,
    title: room.title,
    status: room.status,
    settings: typeof room.settings === 'string' ? JSON.parse(room.settings) : room.settings,
    createdAt: room.createdAt,
    creator: room.creator ? { id: room.creator.id, username: room.creator.username } : null,
    players: (room.players ?? []).map((p: any) => ({
      userId: p.userId,
      username: p.user?.username ?? p.username ?? null,
      isReady: p.isReady,
      isBot: p.isBot ?? p.userId?.startsWith?.('bot_') ?? false,
      joinedAt: p.joinedAt,
    })),
  };
}
