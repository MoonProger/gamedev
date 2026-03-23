import React, { useEffect, useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../services/api';
import Button from '../components/ui/Button';
import Input from '../components/ui/Input';
import Loader from '../components/ui/Loader';
import ErrorMessage from '../components/ui/ErrorMessage';
import { useToast } from '../context/ToastContext';
import { User, UserStats } from '../types/user';
import './Profile.css';

const Profile: React.FC = () => {
  const [user, setUser] = useState<User | null>(null);
  const [stats, setStats] = useState<UserStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [editLoading, setEditLoading] = useState(false);
  const [editData, setEditData] = useState({ username: '' });
  const [avatarPreview, setAvatarPreview] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();
  const { showToast } = useToast();

  useEffect(() => {
    loadProfile();
  }, []);

  const loadProfile = async () => {
    try {
      setLoading(true);
      const token = api.getToken();
      if (!token) {
        navigate('/auth');
        return;
      }

      const userData = await api.getMe();
      setUser(userData.user);
      setEditData({ username: userData.user.username });

      try {
        const statsData = await api.getUserStats(userData.user.id);
        setStats(statsData);
      } catch (statsErr) {
        console.log('Статистика пока недоступна');
        setStats(null);
      }
    } catch (err) {
      setError('Не удалось загрузить профиль');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleEditSubmit = async () => {
    if (!editData.username?.trim()) {
      showToast('Имя не может быть пустым', 'error');
      return;
    }

    try {
      setEditLoading(true);
      
      const updated = await api.updateProfile({ username: editData.username });
      
      setUser(prev => prev ? { ...prev, username: updated.user.username } : null);
      setIsEditing(false);
      setAvatarPreview(null);
      
      showToast('Профиль обновлен!', 'success');
    } catch (err) {
      showToast('Не удалось обновить профиль', 'error');
    } finally {
      setEditLoading(false);
    }
  };

  const handleAvatarChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onloadend = () => {
        setAvatarPreview(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  };

  const removeAvatar = () => {
    setAvatarPreview(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const getInitials = (username: string) => {
    return username.charAt(0).toUpperCase();
  };

  const formatDate = (dateString: string) => {
    try {
      return new Date(dateString).toLocaleDateString('ru-RU', {
        day: 'numeric',
        month: 'long',
        year: 'numeric',
      });
    } catch {
      return 'Неизвестно';
    }
  };

  if (loading) {
    return <Loader text="Загрузка профиля..." fullPage />;
  }

  if (error || !user) {
    return <ErrorMessage message={error || 'Пользователь не найден'} onRetry={() => navigate('/')} fullPage />;
  }

  return (
    <div className="profile-page">
      <div className="profile-header">
        <div className="profile-avatar">
          {avatarPreview || user.avatar ? (
            <img src={avatarPreview || user.avatar || ''} alt={user.username} />
          ) : (
            <div className="avatar-placeholder">
              {getInitials(user.username)}
            </div>
          )}
        </div>

        <div className="profile-info">
          <div className="profile-name-section">
            <h1>{user.username}</h1>
            <button 
              className="edit-profile-btn"
              onClick={() => setIsEditing(true)}
            >
              ✎ Редактировать
            </button>
          </div>
          <div className="profile-email">{user.email}</div>
          <div className="profile-joined">
            <span>Присоединился {formatDate(user.createdAt)}</span>
          </div>
        </div>
      </div>

      {stats ? (
        <div className="profile-stats-grid">
          <div className="stat-card">
            <div className="stat-value">{stats.totalGames}</div>
            <div className="stat-label">Всего игр</div>
          </div>
          <div className="stat-card">
            <div className="stat-value">{stats.wins}</div>
            <div className="stat-label">Побед</div>
          </div>
          <div className="stat-card">
            <div className="stat-value">{stats.losses}</div>
            <div className="stat-label">Поражений</div>
          </div>
          <div className="stat-card">
            <div className="stat-value">{stats.totalScore}</div>
            <div className="stat-label">Всего очков</div>
          </div>
        </div>
      ) : (
        <div className="no-stats">
          <p>У вас пока нет сыгранных игр</p>
          <Button variant="primary" onClick={() => navigate('/rooms')}>
            Найти игру
          </Button>
        </div>
      )}

      {/* Модальное окно редактирования */}
      {isEditing && (
        <div className="edit-modal-overlay" onClick={() => setIsEditing(false)}>
          <div className="edit-modal" onClick={e => e.stopPropagation()}>
            <h2>Редактировать профиль</h2>
            
            <div className="edit-avatar-section">
              <div className="edit-avatar">
                {avatarPreview || user.avatar ? (
                  <img src={avatarPreview || user.avatar || ''} alt="Avatar" />
                ) : (
                  <div className="avatar-placeholder">
                    {getInitials(user.username)}
                  </div>
                )}
              </div>
              <div className="avatar-actions">
                <label htmlFor="avatar-upload">
                  Загрузить
                </label>
                <input
                  ref={fileInputRef}
                  id="avatar-upload"
                  type="file"
                  accept="image/*"
                  onChange={handleAvatarChange}
                  className="hidden-input"
                />
                {(avatarPreview || user.avatar) && (
                  <button onClick={removeAvatar}>
                    Удалить
                  </button>
                )}
              </div>
            </div>

            <Input
              label="Имя пользователя"
              value={editData.username || ''}
              onChange={(e) => setEditData({ ...editData, username: e.target.value })}
              placeholder="Введите имя"
              required
            />

            <div className="edit-modal-actions">
              <Button 
                variant="outline" 
                onClick={() => setIsEditing(false)}
                disabled={editLoading}
              >
                Отмена
              </Button>
              <Button 
                variant="primary" 
                onClick={handleEditSubmit}
                disabled={editLoading}
                isLoading={editLoading}
              >
                Сохранить
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Profile;