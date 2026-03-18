export interface User {
  id: string;
  username: string;
  email: string;
  avatar?: string;
  createdAt: string;
}

export interface UserStats {
  totalGames: number;
  wins: number;
  losses: number;
  winRate: number;
  favoriteSpheres: {
    sphere: string;
    games: number;
  }[];
  recentProgress?: {
    date: string;
    spheres: {
      name: string;
      value: number;
    }[];
  };
}

export interface UpdateProfileData {
  username?: string;
  avatar?: string;
}