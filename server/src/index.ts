import "dotenv/config";
import express from "express";
import { authRoutes } from "./modules/auth/auth.routes";

const app = express();
app.use(express.json());

app.get("/health", (_req, res) => res.json({ ok: true }));

app.use("/auth", authRoutes);

const PORT = Number(process.env.PORT ?? 4000);
app.listen(PORT, () => console.log(`Server listening on :${PORT}`));