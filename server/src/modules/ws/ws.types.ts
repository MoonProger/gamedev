export type WsIn =
  | { type: "room.join"; payload: { roomId: string } }
  | { type: "room.leave"; payload: {} }
  | { type: "room.ready"; payload: { ready: boolean } }
  | { type: "ping"; payload?: any }
  | { type: "game.start"; payload: {} }
  | { type: "game.roll_dice"; payload: {} }
  | { type: "game.move"; payload: { steps: number } };

export type WsOut =
  | { type: "connected"; payload: { userId: string } }
  | { type: "room.state"; payload: any }
  | { type: "room.player_joined"; payload: { userId: string } }
  | { type: "room.player_left"; payload: { userId: string } }
  | { type: "pong"; payload?: any }
  | { type: "error"; payload: { message: string } };
