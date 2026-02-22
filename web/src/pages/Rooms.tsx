import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../services/api';
import Button from '../components/ui/Button';
import './Rooms.css';

interface Room {
  id: string;
  title: string;
  status: 'WAITING' | 'IN_GAME' | 'FINISHED';
  players: Array<{
    userId: string;
    username: string;
    isReady: boolean;
  }>;
  createdAt: string;
  creator: {
    id: string;
    username: string;
  } | null;
}

const Rooms: React.FC = () => {
  const [rooms, setRooms] = useState<Room[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newRoomTitle, setNewRoomTitle] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    loadRooms();
  }, []);

  const loadRooms = async () => {
    try {
      setLoading(true);
      const data = await api.getRooms();
      setRooms(data.rooms || []);
      setError(null);
    } catch (err) {
      setError('Не удалось загрузить комнаты');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const createRoom = async () => {
    if (!newRoomTitle.trim()) return;
    
    try {
      const data = await api.createRoom(newRoomTitle);
      navigate(`/rooms/${data.room.id}`);
    } catch (err) {
      setError('Не удалось создать комнату');
    }
  };

  const joinRoom = async (roomId: string) => {
    try {
      await api.joinRoom(roomId);
      navigate(`/rooms/${roomId}`);
    } catch (err) {
      setError('Не удалось присоединиться к комнате');
    }
  };

  const getStatusText = (status: string) => {
    switch (status) {
      case 'WAITING': return 'Ожидание';
      case 'IN_GAME': return 'В игре';
      case 'FINISHED': return 'Завершена';
      default: return status;
    }
  };

  const getStatusClass = (status: string) => {
    switch (status) {
      case 'WAITING': return 'status-waiting';
      case 'IN_GAME': return 'status-ingame';
      case 'FINISHED': return 'status-finished';
      default: return '';
    }
  };

  if (loading) {
    return (
      <div className="rooms-loading">
        <div className="loader">Загрузка комнат...</div>
      </div>
    );
  }

  return (
    <div className="rooms-page">
      <div className="rooms-header">
        <h1>Игровые комнаты</h1>
        <Button 
          variant="primary" 
          onClick={() => setShowCreateModal(true)}
        >
          + Создать комнату
        </Button>
      </div>

      {error && (
        <div className="error-message">
          {error}
          <button onClick={loadRooms} className="retry-btn">Повторить</button>
        </div>
      )}

      {rooms.length === 0 ? (
        <div className="no-rooms">
          <p>Нет доступных комнат</p>
          <Button variant="outline" onClick={() => setShowCreateModal(true)}>
            Создать первую комнату
          </Button>
        </div>
      ) : (
        <div className="rooms-grid">
          {rooms.map(room => (
            <div key={room.id} className="room-card">
              <div className="room-card-header">
                <h3>{room.title}</h3>
                <span className={`room-status ${getStatusClass(room.status)}`}>
                  {getStatusText(room.status)}
                </span>
              </div>
              
              <div className="room-info">
                <p>Создатель: {room.creator?.username || 'Неизвестно'}</p>
                <p>Игроков: {room.players.length}</p>
                <p>Создана: {new Date(room.createdAt).toLocaleString()}</p>
              </div>

              <div className="room-players">
                <h4>Игроки:</h4>
                {room.players.length === 0 ? (
                  <p className="no-players">Пока нет игроков</p>
                ) : (
                  <ul>
                    {room.players.map(player => (
                      <li key={player.userId}>
                        {player.username} 
                        {player.isReady && <span className="ready-badge">✓ Готов</span>}
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              <div className="room-actions">
                <Button 
                  variant="primary" 
                  fullWidth
                  onClick={() => joinRoom(room.id)}
                  disabled={room.status !== 'WAITING'}
                >
                  {room.status === 'WAITING' ? 'Присоединиться' : 'Игра идёт'}
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {showCreateModal && (
        <div className="modal-overlay" onClick={() => setShowCreateModal(false)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <h2>Создание комнаты</h2>
            <input
              type="text"
              placeholder="Название комнаты"
              value={newRoomTitle}
              onChange={(e) => setNewRoomTitle(e.target.value)}
              className="modal-input"
              autoFocus
            />
            <div className="modal-actions">
              <Button variant="outline" onClick={() => setShowCreateModal(false)}>
                Отмена
              </Button>
              <Button 
                variant="primary" 
                onClick={createRoom}
                disabled={!newRoomTitle.trim()}
              >
                Создать
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Rooms;