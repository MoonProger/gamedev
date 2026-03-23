import { Router } from "express";
import { prisma } from "../../db/prisma";

export const statsRoutes = Router();

statsRoutes.get("/:userId", async (req, res) => {
  const { userId } = req.params;

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

  return res.json({
    stats: {
      userId: stats.userId,
      totalGames: stats.totalGames,
      wins: stats.wins,
      losses: stats.losses,
      totalScore: stats.totalScore,
      favoriteSpheres: JSON.parse(stats.favoriteSpheresJson || "{}"),
    },
  });
});
