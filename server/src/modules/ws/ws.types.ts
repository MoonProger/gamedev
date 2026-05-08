export type JsonMap = Record<string, unknown>;

// NOTE: See protocol source of truth: D:/gamedev/PROTOCOL_UNITY_V1.md
export type WsIn =
  | { type: "room.join"; payload: { roomId: string; password?: string } }
  | { type: "room.leave"; payload: {} }
  | { type: "room.ready"; payload: { ready: boolean } }
  | { type: "ping"; payload?: unknown }
  | { type: "game.start"; payload: {} }
  | { type: "game.roll_dice"; payload: {} }
  | { type: "game.move"; payload: { steps: number; toSector?: number } }
  | {
      type: "game.card";
      payload: {
        // Intent id (optional): server draws authoritative card anyway.
        cardId?: string;
        // Legacy field ignored by server authoritative resolver.
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
        // Optional requested sphere key. Server validates and resolves.
        projectId?: string;
        // Legacy field ignored by server authoritative resolver.
        successPoints?: number;
      };
    }
  | {
      type: "game.card_closed";
      payload: {
        playerId: string;
        cardId: string;
      };
    }
  | {
      type: "game.dev_adjust_stats";
      payload: {
        targetUserId?: string;
        mode?: "delta" | "set";
        deltas: {
          money?: number;
          experience?: number;
          success?: number;
          volounteer?: number;
          science?: number;
          art?: number;
          media?: number;
          business?: number;
          sport?: number;
          tourism?: number;
          it?: number;
          grants?: number;
        };
      };
    }
  | {
      type: "game.character_select";
      payload: {
        characterId?: string;
        stats?: {
          money?: number;
          experience?: number;
          success?: number;
          volounteer?: number;
          science?: number;
          art?: number;
          media?: number;
          business?: number;
          sport?: number;
          tourism?: number;
          it?: number;
        };
      };
    }
  | {
      type: "game.green_choice";
      payload: {
        cardId: string;
        partnerUserId?: string;
      };
    };

export type WsOut =
  | { type: "connected"; payload: { userId: string } }
  | { type: "room.state"; payload: JsonMap }
  | { type: "room.player_joined"; payload: { userId: string } }
  | { type: "room.player_left"; payload: { userId: string } }
  | { type: "pong"; payload?: unknown }
  | { type: "game.started"; payload: { activePlayerId: string } }
  | { type: "game.dice_rolled"; payload: { value: number } }
  | { type: "game.move"; payload: { playerId: string; fromSector: number; toSector: number; dice: number } }
  | { type: "game.card"; payload: JsonMap }
  | { type: "game.project"; payload: JsonMap }
  | { type: "game.finished"; payload: { winnerUserId: string; finalScores: JsonMap } }
  | { type: "game.token_moved"; payload: { playerId: string; pos: number; steps: number } }
  | { type: "game.turn_changed"; payload: { activePlayerId: string } }
  | { type: "game.state"; payload: JsonMap }
  | { type: "game.paused"; payload: { reason: string } }
  | { type: "game.resumed"; payload: {} }
  | { type: "game.card_closed"; payload: { playerId: string; cardId: string } }
  | { type: "error"; payload: { message: string } };