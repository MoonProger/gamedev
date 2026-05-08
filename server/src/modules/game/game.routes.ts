import { Router } from "express";
import { getBoardConfig, getBoardValidationInfo } from "./game.board";

export const gameRoutes = Router();

gameRoutes.get("/board", (_req, res) => {
  const config = getBoardConfig();
  const info = getBoardValidationInfo();
  res.json({
    board: config,
    info,
  });
});

