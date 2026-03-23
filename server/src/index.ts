import "dotenv/config";
import http from "http";
import express from "express";
import cors from "cors";
import { authRoutes } from "./modules/auth/auth.routes";
import { roomsRoutes } from "./modules/rooms/rooms.routes";
import { usersRoutes } from "./modules/users/users.routes";
import { attachWs } from "./modules/ws/ws.server";
import { statsRoutes } from "./modules/stats/stats.routes";

const app = express();
// CORS настройка
app.use(cors({
  origin: ['http://localhost:5173', 'http://127.0.0.1:5173'],
  credentials: true,
}));

app.use(express.json());

app.get("/health", (_req, res) => res.json({ ok: true }));

app.use("/auth", authRoutes);
app.use("/rooms", roomsRoutes);
app.use("/api/users", usersRoutes);

const PORT = Number(process.env.PORT ?? 4000);

const server = http.createServer(app);
attachWs(server);

server.listen(PORT, () => console.log(`HTTP+WS listening on :${PORT}`));