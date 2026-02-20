export type Phase = "WAITING_ROLL" | "WAITING_MOVE";

export type GameState = {
  started: boolean;
  activePlayerId: string | null;
  positions: Record<string, number>;
  lastDice: number | null;
  phase: Phase;
};

const games = new Map<string, GameState>(); // roomId -> state

export function getOrCreateGame(roomId: string): GameState {
  let g = games.get(roomId);
  if (!g) {
    g = {
      started: false,
      activePlayerId: null,
      positions: {},
      lastDice: null,
      phase: "WAITING_ROLL",
    };
    games.set(roomId, g);
  }
  return g;
}

export function resetGame(roomId: string) {
  games.delete(roomId);
}
