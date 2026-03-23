import { prisma } from "../../db/prisma";

export async function saveGameState(roomId: string, game: {
  started: boolean;
  isPaused?: boolean;
  activePlayerId: string | null;
  phase: string;
  lastDice: number | null;
  positions: Record<string, number>;
  scores?: Record<string, any>;
  money?: Record<string, number>;
  experience?: Record<string, number>;
  deckState?: Record<string, any>;
  history?: any[];
}) {
  return prisma.gameState.upsert({
    where: { roomId },
    create: {
      roomId,
      started: game.started,
      isPaused: game.isPaused ?? false,
      activePlayerId: game.activePlayerId,
      phase: game.phase,
      lastDice: game.lastDice,
      positionsJson: JSON.stringify(game.positions ?? {}),
      scoresJson: JSON.stringify(game.scores ?? {}),
      moneyJson: JSON.stringify(game.money ?? {}),
      experienceJson: JSON.stringify(game.experience ?? {}),
      deckStateJson: JSON.stringify(game.deckState ?? {}),
      historyJson: JSON.stringify(game.history ?? []),
    },
    update: {
      started: game.started,
      isPaused: game.isPaused ?? false,
      activePlayerId: game.activePlayerId,
      phase: game.phase,
      lastDice: game.lastDice,
      positionsJson: JSON.stringify(game.positions ?? {}),
      scoresJson: JSON.stringify(game.scores ?? {}),
      moneyJson: JSON.stringify(game.money ?? {}),
      experienceJson: JSON.stringify(game.experience ?? {}),
      deckStateJson: JSON.stringify(game.deckState ?? {}),
      historyJson: JSON.stringify(game.history ?? []),
    },
  });
}

export async function loadGameState(roomId: string) {
  const state = await prisma.gameState.findUnique({
    where: { roomId },
  });

  if (!state) return null;

  return {
    started: state.started,
    isPaused: state.isPaused,
    activePlayerId: state.activePlayerId,
    phase: state.phase,
    lastDice: state.lastDice,
    positions: JSON.parse(state.positionsJson || "{}"),
    scores: JSON.parse(state.scoresJson || "{}"),
    money: JSON.parse(state.moneyJson || "{}"),
    experience: JSON.parse(state.experienceJson || "{}"),
    deckState: JSON.parse(state.deckStateJson || "{}"),
    history: JSON.parse(state.historyJson || "[]"),
  };
}
