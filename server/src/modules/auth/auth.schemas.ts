import { z } from "zod";

export const RegisterSchema = z.object({
  email: z.string().email(),
  username: z.string().min(2).max(32),
  password: z.string().min(6).max(128),
});

export const LoginSchema = z.object({
  email: z.string().email(),
  password: z.string().min(6).max(128),
});
