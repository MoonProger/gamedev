import { Router } from "express";
import { prisma } from "../../db/prisma";

export const statsRoutes = Router();

async function getOrCreateStats(userId: string) {
  const user = await prisma.user.findUnique({
    where: { id: userId },
    select: { id: true },
  });

  if (!user) {
    return null;
  }

  let stats = await prisma.playerStats.findUnique({
    where: { userId },
  });

  if (!stats) {
    stats = await prisma.playerStats.create({
      data: {
        userId,
        totalGames: 0,
        wins: 0,
        losses: 0,
        totalScore: 0,
        favoriteSpheresJson: "{}",
      },
    });
  }

  return stats;
}

function parseJson(value: string | null | undefined, fallback: any) {
  try {
    return value ? JSON.parse(value) : fallback;
  } catch {
    return fallback;
  }
}

statsRoutes.get("/:userId", async (req, res) => {
  const { userId } = req.params;

  const stats = await getOrCreateStats(userId);

  if (!stats) {
    return res.status(404).json({ error: "User not found" });
  }

  return res.json({
    stats: {
      userId: stats.userId,
      totalGames: stats.totalGames,
      wins: stats.wins,
      losses: stats.losses,
      totalScore: stats.totalScore,
      favoriteSpheres: parseJson(stats.favoriteSpheresJson, {}),
    },
  });
});

statsRoutes.get("/:userId/spheres", async (req, res) => {
  const { userId } = req.params;

  const stats = await getOrCreateStats(userId);

  if (!stats) {
    return res.status(404).json({ error: "User not found" });
  }

  return res.json({
    userId: stats.userId,
    favoriteSpheres: parseJson(stats.favoriteSpheresJson, {}),
  });
});