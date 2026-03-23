import { Router } from "express";
import { z } from "zod";
import { prisma } from "../../db/prisma";
import { authRequired } from "../../middlewares/auth";

const UpdateProfileSchema = z.object({
  username: z.string().min(2).max(32).optional(),
  avatar: z.string().url().optional(),
});

export const usersRoutes = Router();

usersRoutes.put("/profile", authRequired, async (req, res) => {
  const parsed = UpdateProfileSchema.safeParse(req.body);
  if (!parsed.success) {
    return res.status(400).json({ error: parsed.error.flatten() });
  }

  const data: { username?: string; avatar?: string } = {};
  if (parsed.data.username !== undefined) data.username = parsed.data.username;
  if (parsed.data.avatar !== undefined) data.avatar = parsed.data.avatar;

  const user = await prisma.user.update({
    where: { id: req.auth!.userId },
    data,
    select: {
      id: true,
      email: true,
      username: true,
      avatar: true,
      createdAt: true,
    },
  });

  return res.json({ user });
});
