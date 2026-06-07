import { z } from "zod";

export const CreateRoomSchema = z.object({
  title: z.string().min(2).max(64),
  password: z.string().min(1).max(64).optional(),
  settings: z
    .object({
      maxPlayers: z.number().int().min(2).max(4).default(4),
      timerSeconds: z.number().int().min(10).max(300).optional(),
    })
    .passthrough()
    .default({ maxPlayers: 4 }),
});

export const ReadySchema = z.object({
  ready: z.boolean(),
});

export const JoinPrivateRoomSchema = z.object({
  password: z.string().min(1).max(64),
});