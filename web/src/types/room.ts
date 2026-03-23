export type RoomStatus = 'WAITING' | 'IN_GAME' | 'FINISHED' | 'CLOSED';

export interface RoomPlayer {
  userId: string;
  username: string;
  isReady: boolean;
  joinedAt?: string;
}

export interface Room {
  id: string;
  title: string;
  status: RoomStatus;
  settings: {
    maxPlayers: number;
    timerSeconds?: number;
    fillWithBots?: boolean;
  };
  password: string | null;
  createdAt: string;
  creator: {
    id: string;
    username: string;
  } | null;
  players: RoomPlayer[];
}

export interface CreateRoomData {
  title: string;
  password?: string;
  settings: {
    maxPlayers: number;
    timerSeconds?: number;
    fillWithBots?: boolean;
  };
}