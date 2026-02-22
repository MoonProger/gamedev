export function roomToDto(room: any) {
  return {
    id: room.id,
    title: room.title,
    status: room.status,
    settings:  typeof room.settings === 'string' ? JSON.parse(room.settings) : room.settings,
    createdAt: room.createdAt,
    creator: room.creator ? { id: room.creator.id, username: room.creator.username } : null,
    players: (room.players ?? []).map((p: any) => ({
      userId: p.userId,
      username: p.user?.username ?? null,
      isReady: p.isReady,
      joinedAt: p.joinedAt,
    })),
  };
}
