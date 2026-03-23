const API_URL = 'http://localhost:4000';

class ApiClient {
  private token: string | null = null;

  constructor() {
    const savedToken = localStorage.getItem('token');
    this.token = savedToken || null;
  }

  setToken(token: string) {
    this.token = token;
    localStorage.setItem('token', token);
  }

  getToken(): string | null {
    return this.token;
  }

  clearToken() {
    this.token = null;
    localStorage.removeItem('token');
  }

  private async request(endpoint: string, options: RequestInit = {}) {
    const url = `${API_URL}${endpoint}`;
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    
    if (options.headers) {
      Object.assign(headers, options.headers);
    }

    const token = this.getToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    try {
      const response = await fetch(url, {
        ...options,
        headers,
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.error || 'Request failed');
      }

      return data;
    } catch (error) {
      if (error instanceof Error) {
        throw error;
      }
      throw new Error('Network error');
    }
  }

  // Auth endpoints
  async register(email: string, username: string, password: string) {
    const data = await this.request('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, username, password }),
    });
    return data;
  }

  async login(email: string, password: string) {
    const data = await this.request('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
    this.setToken(data.token);
    return data;
  }

  async getMe() {
    return this.request('/auth/me');
  }

  // Profile endpoints
  async updateProfile(data: { username?: string; avatar?: string }) {
    return this.request('/api/users/profile', {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async getUserStats(userId: string) {
    const data = await this.request(`/api/stats/${userId}`);
    return data.stats;
  }

  // Rooms endpoints
  async getRooms() {
    return this.request('/rooms');
  }

  async getRoom(id: string) {
    return this.request(`/rooms/${id}`);
  }

  async createRoom(title: string, options?: {
    password?: string;
    settings?: {
      maxPlayers?: number;
      timerSeconds?: number;
      fillWithBots?: boolean;
    }
  }) {
    return this.request('/rooms', {
      method: 'POST',
      body: JSON.stringify({ 
        title,
        password: options?.password,
        settings: {
          maxPlayers: options?.settings?.maxPlayers ?? 4,
          timerSeconds: options?.settings?.timerSeconds,
          fillWithBots: options?.settings?.fillWithBots ?? false,
        }
      }),
    });
  }

  async deleteRoom(id: string) {
    return this.request(`/rooms/${id}`, {
      method: 'DELETE',
    });
  }

  async closeRoom(id: string) {
    return this.request(`/rooms/${id}/close`, {
      method: 'POST',
    });
  }

  async openRoom(id: string) {
    return this.request(`/rooms/${id}/open`, {
      method: 'POST',
    });
  }

  async joinRoom(id: string) {
    return this.request(`/rooms/${id}/join`, {
      method: 'POST',
    });
  }

  async joinPrivateRoom(id: string, password: string) {
    return this.request(`/rooms/${id}/join-private`, {
      method: 'POST',
      body: JSON.stringify({ password }),
    });
  }

  async leaveRoom(id: string) {
    return this.request(`/rooms/${id}/leave`, {
      method: 'POST',
    });
  }

  async setReady(id: string, ready: boolean) {
    return this.request(`/rooms/${id}/ready`, {
      method: 'POST',
      body: JSON.stringify({ ready }),
    });
  }

  async startGame(roomId: string) {
    console.log('Старт игры в комнате:', roomId);
    return this.request(`/rooms/${roomId}/start`, {
      method: 'POST',
    });
  }

  // WebSocket 
  connectWebSocket(onMessage: (data: any) => void): WebSocket {
    const token = this.getToken();
    console.log('Connecting WebSocket with token:', token);
    if (!token) throw new Error('No token');
    
    const ws = new WebSocket(`ws://localhost:4000/ws?token=${token}`);
    
    ws.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        onMessage(data);
      } catch (e) {
        console.error('WS parse error', e);
      }
    };
    
    ws.onerror = (error) => {
      console.error('WS error', error);
    };
    
    return ws;
  }
}

export const api = new ApiClient();