import React, { useEffect, useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../services/api';
import Button from '../components/ui/Button';
import Input from '../components/ui/Input';
import { User, UserStats, UpdateProfileData } from '../types/user';
import './Profile.css';

const Profile: React.FC = () => {
  const [user, setUser] = useState<User | null>(null);
  const [stats, setStats] = useState<UserStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [editLoading, setEditLoading] = useState(false);
  const [editData, setEditData] = useState<UpdateProfileData>({});
  const [avatarFile, setAvatarFile] = useState<File | null>(null);
  const [avatarPreview, setAvatarPreview] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

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

      // Загружаем данные пользователя
      const userData = await api.getMe();
      setUser(userData.user);
      setEditData({ username: userData.user.username });

      // Загружаем статистику (пока заглушка)
      const mockStats: UserStats = {
        totalGames: 24,
        wins: 15,
        losses: 9,
        winRate: 62.5,
        favoriteSpheres: [
          { sphere: 'IT', games: 12 },
          { sphere: 'Предпринимательство', games: 8 },
          { sphere: 'Творчество', games: 6 },
          { sphere: 'Наука', games: 5 },
          { sphere: 'Волонтерство', games: 4 },
        ],
        recentProgress: {
          date: new Date().toISOString(),
          spheres: [
            { name: 'IT', value: 85 },
            { name: 'Бизнес', value: 60 },
            { name: 'Наука', value: 45 },
            { name: 'Творчество', value: 70 },
            { name: 'Спорт', value: 30 },
          ],
        },
      };
      setStats(mockStats);
    } catch (err) {
      setError('Не удалось загрузить профиль');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleEditSubmit = async () => {
    if (!editData.username?.trim()) {
      setError('Имя не может быть пустым');
      return;
    }

    try {
      setEditLoading(true);
      // Здесь будет вызов API для обновления профиля
      // await api.updateProfile(editData);
      
      // Если есть аватар, загружаем его отдельно
      if (avatarFile) {
        // const formData = new FormData();
        // formData.append('avatar', avatarFile);
        // await api.uploadAvatar(formData);
      }

      // Обновляем локальные данные
      setUser(prev => prev ? { ...prev, username: editData.username! } : null);
      setIsEditing(false);
      setAvatarPreview(null);
      setAvatarFile(null);
      
      alert('Профиль обновлен!');
    } catch (err) {
      setError('Не удалось обновить профиль');
    } finally {
      setEditLoading(false);
    }
  };

  const handleAvatarChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setAvatarFile(file);
      const reader = new FileReader();
      reader.onloadend = () => {
        setAvatarPreview(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  };

  const removeAvatar = () => {
    setAvatarFile(null);
    setAvatarPreview(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const getInitials = (username: string) => {
    return username.charAt(0).toUpperCase();
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('ru-RU', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  };

  const maxSphereGames = stats?.favoriteSpheres.length 
    ? Math.max(...stats.favoriteSpheres.map(s => s.games))
    : 1;

  if (loading) {
    return (
      <div className="rooms-loading">
        <div className="loader">Загрузка профиля...</div>
      </div>
    );
  }

  if (error || !user) {
    return (
      <div className="room-error">
        <p>{error || 'Пользователь не найден'}</p>
        <Button variant="primary" onClick={() => navigate('/')}>
          На главную
        </Button>
      </div>
    );
  }

  return (
    <div className="profile-page">
      <div className="profile-header">
        <div className="profile-avatar">
          {avatarPreview || user.avatar ? (
            <img src={avatarPreview || user.avatar} alt={user.username} />
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
        <>
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
              <div className="stat-value">{stats.winRate}%</div>
              <div className="stat-label">Побед</div>
              <div className="stat-trend">+12% за месяц</div>
            </div>
          </div>

          <div className="spheres-section">
            <h2>Любимые сферы</h2>
            <div className="spheres-grid">
              {stats.favoriteSpheres.map((sphere, index) => (
                <div key={index} className="sphere-item">
                  <div className="sphere-header">
                    <span className="sphere-name">{sphere.sphere}</span>
                    <span className="sphere-games">{sphere.games} игр</span>
                  </div>
                  <div className="sphere-bar">
                    <div 
                      className="sphere-fill"
                      style={{ width: `${(sphere.games / maxSphereGames) * 100}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>

          {stats.recentProgress && (
            <div className="spheres-section">
              <h2>Прогресс по сферам (последняя игра)</h2>
              <div className="spheres-grid">
                {stats.recentProgress.spheres.map((sphere, index) => (
                  <div key={index} className="sphere-item">
                    <div className="sphere-header">
                      <span className="sphere-name">{sphere.name}</span>
                      <span className="sphere-games">{sphere.value}%</span>
                    </div>
                    <div className="sphere-bar">
                      <div 
                        className="sphere-fill"
                        style={{ width: `${sphere.value}%` }}
                      />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
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
                  <img src={avatarPreview || user.avatar} alt="Avatar" />
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