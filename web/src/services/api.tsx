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

  // Rooms endpoints
  async getRooms() {
    return this.request('/rooms');
  }

  async getRoom(id: string) {
    return this.request(`/rooms/${id}`);
  }

  async createRoom(title: string, settings?: any) {
    return this.request('/rooms', {
      method: 'POST',
      body: JSON.stringify({ title, settings: settings || { maxPlayers: 5 } }),
    });
  }

  async joinRoom(id: string) {
    return this.request(`/rooms/${id}/join`, {
      method: 'POST',
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
}

export const api = new ApiClient();