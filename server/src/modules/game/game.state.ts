export type Phase = "WAITING_ROLL" | "WAITING_MOVE" | "WAITING_ACTION";

export type GameState = {
  started: boolean;
  isPaused: boolean;
  activePlayerId: string | null;
  positions: Record<string, number>;
  scores: Record<string, Record<string, number>>;
  money: Record<string, number>;
  experience: Record<string, number>;
  deckState: Record<string, any>;
  history: any[];
  lastDice: number | null;
  phase: Phase;
};

const games = new Map<string, GameState>();

export function getOrCreateGame(roomId: string): GameState {
  let g = games.get(roomId);
  if (!g) {
    g = {
      started: false,
      isPaused: false,
      activePlayerId: null,
      positions: {},
      scores: {},
      money: {},
      experience: {},
      deckState: {},
      history: [],
      lastDice: null,
      phase: "WAITING_ROLL",
    };
    games.set(roomId, g);
  }
  return g;
}

export function setGame(roomId: string, game: GameState) {
  games.set(roomId, game);
}

export function resetGame(roomId: string) {
  games.delete(roomId);
}