import { Router } from "express";
import { z } from "zod";
import multer from "multer";
import path from "path";
import fs from "fs";

import { prisma } from "../../db/prisma";
import { authRequired } from "../../middlewares/auth";

const UpdateProfileSchema = z.object({
  username: z.string().min(2).max(32).optional(),
  avatar: z.string().url().optional(),
});

const avatarsDir = path.join(process.cwd(), "uploads", "avatars");

if (!fs.existsSync(avatarsDir)) {
  fs.mkdirSync(avatarsDir, { recursive: true });
}

const storage = multer.diskStorage({
  destination: (_req, _file, cb) => {
    cb(null, avatarsDir);
  },
  filename: (req, file, cb) => {
    const userId = req.auth?.userId ?? "unknown";
    const ext = path.extname(file.originalname).toLowerCase();
    const safeExt = ext || ".png";
    cb(null, `${userId}-${Date.now()}${safeExt}`);
  },
});

const upload = multer({
  storage,
  limits: {
    fileSize: 3 * 1024 * 1024,
  },
  fileFilter: (_req, file, cb) => {
    const allowed = ["image/jpeg", "image/png", "image/webp", "image/gif"];

    if (!allowed.includes(file.mimetype)) {
      cb(new Error("Only image files are allowed"));
      return;
    }

    cb(null, true);
  },
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

usersRoutes.post(
  "/avatar",
  authRequired,
  upload.single("avatar"),
  async (req, res) => {
    if (!req.file) {
      return res.status(400).json({ error: "Avatar file is required" });
    }

    const avatarUrl = `/uploads/avatars/${req.file.filename}`;

    const user = await prisma.user.update({
      where: { id: req.auth!.userId },
      data: {
        avatar: avatarUrl,
      },
      select: {
        id: true,
        email: true,
        username: true,
        avatar: true,
        createdAt: true,
      },
    });

    return res.json({
      avatar: avatarUrl,
      user,
    });
  }
);