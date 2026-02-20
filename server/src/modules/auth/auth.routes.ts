import { Router } from "express";
import { RegisterSchema, LoginSchema } from "./auth.schemas";
import { register, login } from "./auth.service";
import { authRequired } from "../../middlewares/auth";
import { prisma } from "../../db/prisma";

export const authRoutes = Router();

authRoutes.post("/register", async (req, res) => {
  const parsed = RegisterSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: parsed.error.flatten() });

  try {
    const user = await register(parsed.data.email, parsed.data.username, parsed.data.password);
    return res.json({ user });
  } catch (e: any) {
    if (e.message === "EMAIL_TAKEN") return res.status(409).json({ error: "Email already used" });
    return res.status(500).json({ error: "Server error" });
  }
});

authRoutes.post("/login", async (req, res) => {
  const parsed = LoginSchema.safeParse(req.body);
  if (!parsed.success) return res.status(400).json({ error: parsed.error.flatten() });

  try {
    const out = await login(parsed.data.email, parsed.data.password);
    return res.json(out);
  } catch (e: any) {
    if (e.message === "INVALID_CREDENTIALS") return res.status(401).json({ error: "Invalid credentials" });
    return res.status(500).json({ error: "Server error" });
  }
});

authRoutes.get("/me", authRequired, async (req, res) => {
  const user = await prisma.user.findUnique({
    where: { id: req.auth!.userId },
    select: { id: true, email: true, username: true, createdAt: true },
  });
  return res.json({ user });
});
