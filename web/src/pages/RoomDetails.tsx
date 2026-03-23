import React, { useEffect, useState, useCallback } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import { api } from '../services/api';
import Button from '../components/ui/Button';
import Loader from '../components/ui/Loader';
import ErrorMessage from '../components/ui/ErrorMessage';
import { useToast } from '../context/ToastContext';
import { useWebSocket } from '../hooks/useWebSocket';
import { Room } from '../types/room';
import './RoomDetails.css';

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
  const navigate = useNavigate();
  const location = useLocation();

  const statePassword = (location.state as any)?.roomPassword;
  const savedPassword = id ? localStorage.getItem(`room_${id}_password`) : null;
  const initialPassword = statePassword || savedPassword || null;
  
  const [room, setRoom] = useState<Room | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isLeaving, setIsLeaving] = useState(false);
  const [isReadyLoading, setIsReadyLoading] = useState(false);
  const [isStarting] = useState(false);
  const [isClosing, setIsClosing] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  
  const [roomPassword] = useState<string | null>(() => {
    if (initialPassword && id) {
      localStorage.removeItem(`room_${id}_password`);
    }
    return initialPassword;
  });
  
  console.log('RoomDetails: id =', id);
  console.log('RoomDetails: location.state =', location.state);
  console.log('RoomDetails: savedPassword from localStorage =', savedPassword);
  console.log('RoomDetails: roomPassword =', roomPassword);
  
  const { showToast } = useToast();

  const handleWebSocketMessage = useCallback((data: any) => {
    console.log('WS message:', data);
    
    switch (data.type) {
      case 'room.state':
        setRoom(data.payload);
        break;
      case 'game.started':
        showToast('Игра началась!', 'success');
        navigate(`/game/${id}`);
        break;
      case 'game.paused':
        showToast(`Игра на паузе: ${data.payload.reason}`, 'info');
        break;
      case 'error':
        showToast(data.payload.message, 'error');
        break;
    }
  }, [navigate, showToast, id]);

  const loadRoom = async () => {
    if (!id) return;
    
    try {
      setLoading(true);
      const data = await api.getRoom(id);
      setRoom(data.room);
      setError(null);
    } catch (err) {
      setError('Не удалось загрузить комнату');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRoom();
  }, [id]);

  // Вычисляем, нужно ли пропускать автоматический join
  const currentUserId = getUserIdFromToken();
  const isCreator = room?.creator?.id === currentUserId;
  const isAlreadyInRoom = room?.players?.some(p => p.userId === currentUserId) ?? false;
  const shouldSkipJoin = isCreator || isAlreadyInRoom;

  // Передаём shouldSkipJoin в useWebSocket
const { send, isConnected } = useWebSocket(id || null, roomPassword, handleWebSocketMessage, shouldSkipJoin);
  const handleReady = async () => {
    if (!room || !id) return;
    
    const currentPlayer = room.players?.find(p => p.userId === getUserIdFromToken());
    if (!currentPlayer) return;
    
    setIsReadyLoading(true);
    try {
      await api.setReady(id, !currentPlayer.isReady);
      showToast(currentPlayer.isReady ? 'Готовность отменена' : 'Вы готовы!', 'success');
      await loadRoom();
    } catch (err) {
      showToast('Не удалось изменить статус', 'error');
    } finally {
      setIsReadyLoading(false);
    }
  };

  const handleLeave = async () => {
    if (!id) return;
    
    try {
      setIsLeaving(true);
      await api.leaveRoom(id);
      showToast('Вы покинули комнату', 'info');
      navigate('/rooms');
    } catch (err) {
      showToast('Не удалось покинуть комнату', 'error');
      setIsLeaving(false);
    }
  };

  const handleStartGame = async () => {
  if (!id) return;
  console.log('Starting game, waiting for WebSocket connection...');
  
  let attempts = 0;
  while (!isConnected && attempts < 30) {
    await new Promise(resolve => setTimeout(resolve, 100));
    attempts++;
  }
  
  if (!isConnected) {
    console.error('WebSocket not connected after 3 seconds');
    showToast('Ошибка подключения', 'error');
    return;
  }
  
  console.log('Sending room.join');
  send('room.join', { roomId: id, password: roomPassword });
  
  setTimeout(() => {
    console.log('Now sending game.start');
    send('game.start', {});
  }, 200);
};

  const handleCloseRoom = async () => {
    if (!id) return;
    
    try {
      setIsClosing(true);
      await api.closeRoom(id);
      showToast('Комната закрыта', 'success');
      await loadRoom();
    } catch (err) {
      showToast('Не удалось закрыть комнату', 'error');
    } finally {
      setIsClosing(false);
    }
  };

  const handleOpenRoom = async () => {
    if (!id) return;
    
    try {
      setIsClosing(true);
      await api.openRoom(id);
      showToast('Комната открыта', 'success');
      await loadRoom();
    } catch (err) {
      showToast('Не удалось открыть комнату', 'error');
    } finally {
      setIsClosing(false);
    }
  };

  const handleDeleteRoom = async () => {
    if (!id) return;
    
    try {
      setIsDeleting(true);
      await api.deleteRoom(id);
      showToast('Комната удалена', 'success');
      navigate('/rooms');
    } catch (err) {
      showToast('Не удалось удалить комнату', 'error');
      setIsDeleting(false);
    }
    setShowDeleteConfirm(false);
  };

  const currentPlayer = room?.players?.find(p => p.userId === currentUserId);
  const allReady = (room?.players?.length ?? 0) >= 2 && room?.players?.every(p => p.isReady);
  const maxPlayers = room?.settings?.maxPlayers ?? 4;

  if (loading) {
    return <Loader text="Загрузка комнаты..." fullPage />;
  }

  if (error || !room) {
    return <ErrorMessage message={error || 'Комната не найдена'} onRetry={() => navigate('/rooms')} fullPage />;
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
          <span className={`room-status ${room.status?.toLowerCase() || ''}`}>
            {room.status === 'WAITING' ? 'Ожидание игроков' : 
             room.status === 'IN_GAME' ? 'Игра началась' : 
             room.status === 'CLOSED' ? 'Комната закрыта' :
             room.status === 'FINISHED' ? 'Игра завершена' :
             'Неизвестный статус'}
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
            <span className="info-value">{room.players?.length || 0}/{maxPlayers}</span>
          </div>
          {room.settings?.timerSeconds && (
            <div className="info-row">
              <span className="info-label">Время на ход:</span>
              <span className="info-value">{room.settings.timerSeconds} сек</span>
            </div>
          )}
        </div>

        <div className="players-section">
          <h2>Игроки в комнате</h2>
          <div className="players-list">
            {room.players?.map(player => (
              <div key={player.userId} className="player-item">
                <div className="player-info">
                  <span className="player-name">{player.username || 'Без имени'}</span>
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
            {Array.from({ length: maxPlayers - (room.players?.length ?? 0) }).map((_, i) => (
              <div key={`empty-${i}`} className="player-item empty-slot">
                <div className="player-info">
                  <span className="player-name">Свободное место</span>
                </div>
                <div className="player-status">
                  <span className="ready-status">Ожидает игрока</span>
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

          {isCreator && (
            <div className="creator-actions" style={{ display: 'flex', gap: '0.5rem', marginLeft: 'auto' }}>
              {room.status === 'WAITING' && (
                <Button
                  variant="outline"
                  onClick={handleCloseRoom}
                  disabled={isClosing}
                  isLoading={isClosing}
                  style={{ borderColor: '#f59e0b', color: '#f59e0b' }}
                >
                  Закрыть комнату
                </Button>
              )}
              {room.status === 'CLOSED' && (
                <Button
                  variant="outline"
                  onClick={handleOpenRoom}
                  disabled={isClosing}
                  isLoading={isClosing}
                  style={{ borderColor: '#10b981', color: '#10b981' }}
                >
                  Открыть комнату
                </Button>
              )}
              <Button
                variant="danger"
                onClick={() => setShowDeleteConfirm(true)}
                disabled={isDeleting}
              >
                Удалить комнату
              </Button>
            </div>
          )}
        </div>
      </div>
      
      {showDeleteConfirm && (
        <div className="modal-overlay" onClick={() => setShowDeleteConfirm(false)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <h2 style={{ color: '#ef4444' }}>Удаление комнаты</h2>
            <p style={{ marginBottom: '1.5rem', color: '#666' }}>
              Вы уверены, что хотите удалить комнату "{room.title}"?<br />
              Это действие нельзя отменить.
            </p>
            <div className="modal-actions" style={{ justifyContent: 'flex-end' }}>
              <Button 
                variant="outline" 
                onClick={() => setShowDeleteConfirm(false)}
                disabled={isDeleting}
              >
                Отмена
              </Button>
              <Button 
                variant="danger" 
                onClick={handleDeleteRoom}
                disabled={isDeleting}
                isLoading={isDeleting}
              >
                Удалить
              </Button>
            </div>
          </div>
        </div>
      )}
    </div> 
  );
};

export default RoomDetails;