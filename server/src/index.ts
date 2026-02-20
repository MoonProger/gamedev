import "dotenv/config";
import http from "http";
import express from "express";
import { authRoutes } from "./modules/auth/auth.routes";
import { roomsRoutes } from "./modules/rooms/rooms.routes";
import { attachWs } from "./modules/ws/ws.server";

const app = express();
app.use(express.json());

app.get("/health", (_req, res) => res.json({ ok: true }));

app.use("/auth", authRoutes);
app.use("/rooms", roomsRoutes);

const PORT = Number(process.env.PORT ?? 4000);

const server = http.createServer(app);
attachWs(server);

server.listen(PORT, () => console.log(`HTTP+WS listening on :${PORT}`));