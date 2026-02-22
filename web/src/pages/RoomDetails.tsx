import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '../services/api';
import Button from '../components/ui/Button';
import './RoomDetails.css';

interface Room {
  id: string;
  title: string;
  status: 'WAITING' | 'IN_GAME' | 'FINISHED';
  players: Array<{
    userId: string;
    user: {
      id: string;
      username: string;
    };
    isReady: boolean;
    joinedAt: string;
  }>;
  createdAt: string;
  creator: {
    id: string;
    username: string;
  } | null;
}

function getUserIdFromToken(): string | null {
  const token = api.getToken();
  if (!token) return null;
  
  try {
    const parts = token.split('.');
    const payload = JSON.parse(atob(parts[1]));
    return payload.userId;
  } catch (e) {
    console.error('Ошибка декодирования токена:', e);
    return null;
  }
}

const RoomDetails: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const [room, setRoom] = useState<Room | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isLeaving, setIsLeaving] = useState(false);
  const [isReadyLoading, setIsReadyLoading] = useState(false);
  const [isStarting, setIsStarting] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    if (id) {
      loadRoom();
    }
  }, [id]);

  const loadRoom = async () => {
    if (!id) return;
    
    try {
      setLoading(true);
      const data = await api.getRoom(id);
      console.log('Данные комнаты:', data);
      setRoom(data.room);
      setError(null);
    } catch (err) {
      setError('Не удалось загрузить комнату');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleReady = async () => {
    if (!room || !id) return;
    
    const currentPlayer = room.players.find(p => p.userId === getUserIdFromToken());
    if (!currentPlayer) return;
    
    setIsReadyLoading(true);
    try {
      await api.setReady(id, !currentPlayer.isReady);
      await loadRoom();
    } catch (err) {
      setError('Не удалось изменить статус');
    } finally {
      setIsReadyLoading(false);
    }
  };

  const handleLeave = async () => {
    if (!id) return;
    
    try {
      setIsLeaving(true);
      await api.leaveRoom(id);
      navigate('/rooms');
    } catch (err) {
      setError('Не удалось покинуть комнату');
      setIsLeaving(false);
    }
  };

  const handleStartGame = () => {
    console.log('Начинаем игру в комнате:', id);
    if (!id) return;
    navigate(`/game/${id}`);
  };

  const currentUserId = getUserIdFromToken();
  const currentPlayer = room?.players.find(p => p.userId === currentUserId);
  const isCreator = room?.creator?.id === currentUserId;
  const allReady = room?.players.length >= 2 && room?.players.every(p => p.isReady);

  if (loading) {
    return (
      <div className="room-loading">
        <div className="loader">Загрузка комнаты...</div>
      </div>
    );
  }

  if (error || !room) {
    return (
      <div className="room-error">
        <p>{error || 'Комната не найдена'}</p>
        <Button variant="primary" onClick={() => navigate('/rooms')}>
          Вернуться к комнатам
        </Button>
      </div>
    );
  }

  return (
    <div className="room-details">
      <div className="room-details-header">
        <Button 
          variant="outline" 
          onClick={() => navigate('/rooms')}
          className="back-button"
        >
          Назад к комнатам
        </Button>
        <div className="room-title-section">
          <h1>{room.title}</h1>
          <span className={`room-status ${room.status.toLowerCase()}`}>
            {room.status === 'WAITING' ? 'Ожидание игроков' : 
             room.status === 'IN_GAME' ? 'Игра началась' : 'Игра завершена'}
          </span>
        </div>
      </div>

      <div className="room-content">
        <div className="room-info-card">
          <h2>Информация о комнате</h2>
          <div className="info-row">
            <span className="info-label">Создатель:</span>
            <span className="info-value">{room.creator?.username || 'Неизвестно'}</span>
          </div>
          <div className="info-row">
            <span className="info-label">Дата создания:</span>
            <span className="info-value">{new Date(room.createdAt).toLocaleString()}</span>
          </div>
          <div className="info-row">
            <span className="info-label">Игроков:</span>
            <span className="info-value">{room.players.length}</span>
          </div>
        </div>

        <div className="players-section">
          <h2>Игроки в комнате</h2>
          <div className="players-list">
            {room.players.map(player => (
              <div key={player.userId} className="player-item">
                <div className="player-info">
                  <span className="player-name">{player.user?.username || 'Без имени'}</span>
                  {player.userId === room.creator?.id && (
                    <span className="player-creator">Создатель</span>
                  )}
                  {player.userId === currentUserId && (
                    <span className="player-you">(Вы)</span>
                  )}
                </div>
                <div className="player-status">
                  {player.isReady ? (
                    <span className="ready-status ready">Готов</span>
                  ) : (
                    <span className="ready-status not-ready">Не готов</span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="room-actions">
          {room.status === 'WAITING' && currentPlayer && (
            <Button
              variant={currentPlayer.isReady ? 'outline' : 'primary'}
              onClick={handleReady}
              disabled={isReadyLoading || isLeaving}
              isLoading={isReadyLoading}
            >
              {currentPlayer.isReady ? 'Отменить готовность' : 'Я готов'}
            </Button>
          )}
          
          {isCreator && allReady && room.status === 'WAITING' && (
            <Button
              variant="primary"
              onClick={handleStartGame}
              disabled={isStarting}
            >
              Начать игру
            </Button>
          )}

          <Button
            variant="outline"
            onClick={handleLeave}
            disabled={isLeaving}
            isLoading={isLeaving}
          >
            Покинуть комнату
          </Button>
        </div>
      </div>
    </div>
  );
};

export default RoomDetails;