export type WsIn =
  | { type: "room.join"; payload: { roomId: string; password?: string } }
  | { type: "room.leave"; payload: {} }
  | { type: "room.ready"; payload: { ready: boolean } }
  | { type: "ping"; payload?: any }
  | { type: "game.start"; payload: {} }
  | { type: "game.roll_dice"; payload: {} }
  | { type: "game.move"; payload: { steps: number } }
  | {
      type: "game.card";
      payload: {
        cardId: string;
        effects?: {
          money?: number;
          experience?: number;
          successPoints?: number;
          sphereScores?: Record<string, number>;
        };
      };
    }
  | {
      type: "game.project";
      payload: {
        projectId: string;
        successPoints: number;
      };
    };

export type WsOut =
  | { type: "connected"; payload: { userId: string } }
  | { type: "room.state"; payload: any }
  | { type: "room.player_joined"; payload: { userId: string } }
  | { type: "room.player_left"; payload: { userId: string } }
  | { type: "pong"; payload?: any }
  | { type: "game.started"; payload: any }
  | { type: "game.dice_rolled"; payload: { value: number } }
  | { type: "game.move"; payload: { playerId: string; fromSector: number; toSector: number; dice: number } }
  | { type: "game.card"; payload: any }
  | { type: "game.project"; payload: any }
  | { type: "game.token_moved"; payload: any }
  | { type: "game.turn_changed"; payload: { activePlayerId: string } }
  | { type: "game.state"; payload: any }
  | { type: "game.paused"; payload: { reason: string } }
  | { type: "game.resumed"; payload: {} }
  | { type: "error"; payload: { message: string } };