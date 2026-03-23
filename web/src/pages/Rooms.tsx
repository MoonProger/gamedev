import React, { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../services/api';
import Button from '../components/ui/Button';
import Input from '../components/ui/Input';
import Loader from '../components/ui/Loader';
import ErrorMessage from '../components/ui/ErrorMessage';
import { useToast } from '../context/ToastContext';
import { Room, RoomStatus } from '../types/room';
import './Rooms.css';

const Rooms: React.FC = () => {
  const [rooms, setRooms] = useState<Room[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  
  // Фильтры и поиск
  const [statusFilter, setStatusFilter] = useState<RoomStatus | 'ALL'>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  
  // Модалки
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showPrivateModal, setShowPrivateModal] = useState<{roomId: string, roomTitle: string} | null>(null);
  
  // Создание комнаты
  const [newRoom, setNewRoom] = useState({
    title: '',
    password: '',
    maxPlayers: 4,
    timerSeconds: 30,
    fillWithBots: false
  });
  const [timerEnabled, setTimerEnabled] = useState(false);
  const [creating, setCreating] = useState(false);

  const [passwordInput, setPasswordInput] = useState('');
  const [joinError, setJoinError] = useState<string | null>(null);
  
  const navigate = useNavigate();
  const { showToast } = useToast();

  const loadRooms = useCallback(async () => {
    try {
      setLoading(true);
      const data = await api.getRooms();
      setRooms(data.rooms || []);
      setError(null);
    } catch (err) {
      setError('Не удалось загрузить комнаты');
      console.error('Ошибка загрузки комнат:', err);
      showToast('Ошибка загрузки комнат', 'error');
    } finally {
      setLoading(false);
    }
  }, [showToast]);

  useEffect(() => {
    loadRooms();
  }, [loadRooms]);

  // Фильтрация комнат
  const filteredRooms = rooms.filter(room => {
    if (statusFilter !== 'ALL' && room.status !== statusFilter) return false;
    if (searchQuery && !room.title.toLowerCase().includes(searchQuery.toLowerCase())) return false;
    return true;
  });

  const createRoom = async () => {
    if (!newRoom.title.trim()) {
      showToast('Введите название комнаты', 'error');
      return;
    }
    
    setCreating(true);
    try {
      const data = await api.createRoom(newRoom.title, {
        password: newRoom.password || undefined,
        settings: {
          maxPlayers: newRoom.maxPlayers,
          timerSeconds: timerEnabled ? newRoom.timerSeconds : undefined,
          fillWithBots: newRoom.fillWithBots
        }
      });
      
      showToast('Комната создана!', 'success');
      
      if (newRoom.password) {
        const key = `room_${data.room.id}_password`;
        localStorage.setItem(key, newRoom.password);
      }
      
      navigate(`/rooms/${data.room.id}`);
    } catch (err) {
      console.error('Ошибка создания комнаты:', err);
      showToast('Не удалось создать комнату', 'error');
    } finally {
      setCreating(false);
    }
  };

  const joinRoom = async (roomId: string, isPrivate: boolean) => {
    if (isPrivate) {
      const room = rooms.find(r => r.id === roomId);
      if (room) {
        setShowPrivateModal({ roomId, roomTitle: room.title });
        setPasswordInput('');
        setJoinError(null);
      }
      return;
    }
    
    try {
      await api.joinRoom(roomId);
      navigate(`/rooms/${roomId}`);
    } catch (err: any) {
      showToast(err.message || 'Не удалось присоединиться', 'error');
    }
  };

  const joinPrivateRoom = async () => {
    if (!showPrivateModal || !passwordInput.trim()) return;
    
    try {
      await api.joinPrivateRoom(showPrivateModal.roomId, passwordInput);
      setShowPrivateModal(null);
      navigate(`/rooms/${showPrivateModal.roomId}`, {
        state: { roomPassword: passwordInput }
      });
    } catch (err) {
      console.error('Ошибка входа в приватную комнату:', err);
      setJoinError('Неверный пароль');
    }
  };

  const quickJoin = () => {
    const availableRoom = rooms.find(r => 
      r.status === 'WAITING' && 
      !r.password && 
      r.players.length < (r.settings?.maxPlayers ?? 4)
    );
    
    if (availableRoom) {
      joinRoom(availableRoom.id, false);
    } else {
      setShowCreateModal(true);
    }
  };

  const getStatusText = (status: RoomStatus) => {
    switch (status) {
      case 'WAITING': return 'Ожидание';
      case 'IN_GAME': return 'В игре';
      case 'CLOSED': return 'Закрыта';
      case 'FINISHED': return 'Завершена';
      default: return status;
    }
  };

  const getStatusClass = (status: RoomStatus) => {
    switch (status) {
      case 'WAITING': return 'status-waiting';
      case 'IN_GAME': return 'status-ingame';
      case 'CLOSED': return 'status-closed';
      case 'FINISHED': return 'status-finished';
      default: return '';
    }
  };

  if (loading) {
    return <Loader text="Загрузка комнат..." fullPage />;
  }

  if (error) {
    return <ErrorMessage message={error} onRetry={loadRooms} fullPage />;
  }

  return (
    <div className="rooms-page">
      <div className="rooms-header">
        <h1>Игровые комнаты</h1>
        <div className="header-actions">
          <Button variant="primary" onClick={quickJoin}>
            Быстрая игра
          </Button>
          <Button variant="primary" onClick={() => setShowCreateModal(true)}>
            + Создать комнату
          </Button>
        </div>
      </div>

      {/* Фильтры и поиск */}
      <div className="rooms-filters">
        <div className="filter-tabs">
          <button 
            className={`filter-tab ${statusFilter === 'ALL' ? 'active' : ''}`}
            onClick={() => setStatusFilter('ALL')}
          >
            Все
          </button>
          <button 
            className={`filter-tab ${statusFilter === 'WAITING' ? 'active' : ''}`}
            onClick={() => setStatusFilter('WAITING')}
          >
            Ожидание
          </button>
          <button 
            className={`filter-tab ${statusFilter === 'IN_GAME' ? 'active' : ''}`}
            onClick={() => setStatusFilter('IN_GAME')}
          >
            В игре
          </button>
          <button 
            className={`filter-tab ${statusFilter === 'CLOSED' ? 'active' : ''}`}
            onClick={() => setStatusFilter('CLOSED')}
          >
            Закрытые
          </button>
        </div>
        
        <div className="search-box">
          <input
            type="text"
            placeholder="Поиск по названию..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="search-input"
          />
        </div>
      </div>

      {filteredRooms.length === 0 ? (
        <div className="no-rooms">
          <p>Комнаты не найдены</p>
          <Button variant="outline" onClick={() => setShowCreateModal(true)}>
            Создать комнату
          </Button>
        </div>
      ) : (
        <div className="rooms-grid">
          {filteredRooms.map(room => (
            <div key={room.id} className="room-card">
              <div className="room-card-header">
                <h3>
                  {room.title}
                  {room.password && <span className="private-badge">Приватная</span>}
                </h3>
                <span className={`room-status ${getStatusClass(room.status)}`}>
                  {getStatusText(room.status)}
                </span>
              </div>
              
              <div className="room-info">
                <p>Создатель: {room.creator?.username || 'Неизвестно'}</p>
                <p>Игроков: {room.players.length}/{room.settings?.maxPlayers ?? 4}</p>
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
                        {player.isReady && <span className="ready-badge">Готов</span>}
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              <div className="room-actions">
                <Button 
                  variant="primary" 
                  fullWidth
                  onClick={() => joinRoom(room.id, !!room.password)}
                  disabled={room.status !== 'WAITING'}
                >
                  {room.status === 'WAITING' 
                    ? (room.password ? 'Ввести пароль' : 'Присоединиться')
                    : 'Игра идёт'}
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Модалка создания комнаты */}
      {showCreateModal && (
        <div className="modal-overlay" onClick={() => setShowCreateModal(false)}>
          <div className="modal-content create-room-modal" onClick={e => e.stopPropagation()}>
            <h2>Создание комнаты</h2>
            
            <Input
              label="Название комнаты"
              value={newRoom.title}
              onChange={(e) => setNewRoom({...newRoom, title: e.target.value})}
              placeholder="Введите название"
              required
            />

            <div className="private-room-toggle">
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={!!newRoom.password}
                  onChange={(e) => setNewRoom({...newRoom, password: e.target.checked ? '123' : ''})}
                />
                Приватная комната (только по паролю)
              </label>
            </div>

            {newRoom.password && (
              <Input
                label="Пароль"
                type="password"
                value={newRoom.password}
                onChange={(e) => setNewRoom({...newRoom, password: e.target.value})}
                placeholder="Введите пароль"
                required
              />
            )}

            <div className="settings-group">
              <label>Количество игроков</label>
              <div className="radio-group">
                {[3, 4, 5].map(num => (
                  <label key={num} className="radio-label">
                    <input
                      type="radio"
                      name="maxPlayers"
                      value={num}
                      checked={newRoom.maxPlayers === num}
                      onChange={(e) => setNewRoom({...newRoom, maxPlayers: Number(e.target.value)})}
                    />
                    {num}
                  </label>
                ))}
              </div>
            </div>

            <div className="settings-group">
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={timerEnabled}
                  onChange={(e) => setTimerEnabled(e.target.checked)}
                />
                Ограничить время на ход
              </label>
            </div>

            {timerEnabled && (
              <div className="settings-group">
                <label>Время на ход (сек)</label>
                <div className="radio-group">
                  {[30, 60, 90].map(sec => (
                    <label key={sec} className="radio-label">
                      <input
                        type="radio"
                        name="timerSeconds"
                        value={sec}
                        checked={newRoom.timerSeconds === sec}
                        onChange={(e) => setNewRoom({...newRoom, timerSeconds: Number(e.target.value)})}
                      />
                      {sec} сек
                    </label>
                  ))}
                </div>
              </div>
            )}

            <div className="settings-group">
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={newRoom.fillWithBots}
                  onChange={(e) => setNewRoom({...newRoom, fillWithBots: e.target.checked})}
                />
                Заполнить ботами
              </label>
            </div>

            <div className="modal-actions">
              <Button variant="outline" onClick={() => setShowCreateModal(false)}>
                Отмена
              </Button>
              <Button 
                variant="primary" 
                onClick={createRoom}
                disabled={!newRoom.title.trim() || creating}
                isLoading={creating}
              >
                Создать
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Модалка ввода пароля */}
      {showPrivateModal && (
        <div className="modal-overlay" onClick={() => setShowPrivateModal(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <h2>Приватная комната</h2>
            <p>Введите пароль для входа в комнату "{showPrivateModal.roomTitle}"</p>
            
            <Input
              type="password"
              value={passwordInput}
              onChange={(e) => setPasswordInput(e.target.value)}
              placeholder="Пароль"
              error={joinError || undefined}
              autoFocus
            />

            <div className="modal-actions">
              <Button variant="outline" onClick={() => setShowPrivateModal(null)}>
                Отмена
              </Button>
              <Button 
                variant="primary" 
                onClick={joinPrivateRoom}
                disabled={!passwordInput.trim()}
              >
                Войти
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Rooms;