export type RoomStatus = 'WAITING' | 'IN_GAME' | 'FINISHED' | 'CLOSED';

export interface Player {
  userId: string;
  username: string; 
  isReady: boolean;
  joinedAt?: string;
}

export interface Room {
  id: string;
  title: string;
  status: RoomStatus;
  isPrivate?: boolean;
  password?: string;
  players: Player[];  
  createdAt: string;
  creator: {
    id: string;
    username: string;
  } | null;
  settings?: {
    maxPlayers: number;
    timePerMove?: 'unlimited' | '30' | '60';
    mode?: 'training' | 'standard' | 'tournament';
    fillWithBots?: boolean;
  };
}