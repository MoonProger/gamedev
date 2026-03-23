import { prisma } from "../../db/prisma";

export async function finalizeGame(roomId: string, winnerUserId: string, game: any) {
  const room = await prisma.room.findUnique({
    where: { id: roomId },
    include: { players: true },
  });

  if (!room) return;

  for (const player of room.players) {
    const userId = player.userId;
    const playerScores = game.scores?.[userId] ?? {};
    const totalScore = Object.values(playerScores).reduce(
      (sum: number, v: any) => sum + Number(v || 0),
      0
    );

    const existing = await prisma.playerStats.findUnique({
      where: { userId },
    });

    const favoriteSpheres = existing?.favoriteSpheresJson
      ? JSON.parse(existing.favoriteSpheresJson)
      : {};

    for (const [sphere, value] of Object.entries(playerScores)) {
      favoriteSpheres[sphere] = (favoriteSpheres[sphere] ?? 0) + Number(value || 0);
    }

    await prisma.playerStats.upsert({
      where: { userId },
      create: {
        userId,
        totalGames: 1,
        wins: userId === winnerUserId ? 1 : 0,
        losses: userId === winnerUserId ? 0 : 1,
        totalScore,
        favoriteSpheresJson: JSON.stringify(favoriteSpheres),
      },
      update: {
        totalGames: { increment: 1 },
        wins: userId === winnerUserId ? { increment: 1 } : undefined,
        losses: userId !== winnerUserId ? { increment: 1 } : undefined,
        totalScore: { increment: totalScore },
        favoriteSpheresJson: JSON.stringify(favoriteSpheres),
      },
    });
  }

  await prisma.room.update({
    where: { id: roomId },
    data: { status: "FINISHED" },
  });
}
