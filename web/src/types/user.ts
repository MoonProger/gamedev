export interface User {
  id: string;
  email: string;
  username: string;
  avatar: string | null;
  createdAt: string;
}

export interface UserStats {
  totalGames: number;
  wins: number;
  losses: number;
  totalScore: number;
  favoriteSpheres: Record<string, number>;
}