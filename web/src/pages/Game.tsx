import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Unity, useUnityContext } from "react-unity-webgl";
import { api } from '../services/api';
import Button from '../components/ui/Button';
import './Game.css';

interface Player {
  userId: string;
  user: {
    id: string;
    username: string;
  };
  isReady: boolean;
}

const Game: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [players, setPlayers] = useState<Player[]>([]);
  const [loading, setLoading] = useState(true);

  const { 
    unityProvider, 
    isLoaded, 
    loadingProgression, 
    error,
    sendMessage,
    initialisationError 
  } = useUnityContext({
    loaderUrl: "/Build/WebGL_Build.loader.js",
    dataUrl: "/Build/WebGL_Build.data.gz",
    frameworkUrl: "/Build/WebGL_Build.framework.js.gz",
    codeUrl: "/Build/WebGL_Build.wasm.gz",
  });

  // Отслеживаем все состояния Unity
  useEffect(() => {
    console.log('🔄 Unity состояние:', {
      isLoaded,
      loadingProgression,
      error: error?.message,
      initialisationError
    });
  }, [isLoaded, loadingProgression, error, initialisationError]);

  // Загружаем информацию о комнате
  useEffect(() => {
    if (id) {
      loadRoomData();
    }
  }, [id]);

  const loadRoomData = async () => {
    try {
      const data = await api.getRoom(id!);
      console.log('📦 Данные комнаты загружены:', data.room.players);
      setPlayers(data.room.players);
    } catch (err) {
      console.error('❌ Ошибка загрузки комнаты:', err);
    } finally {
      setLoading(false);
    }
  };

  // Когда Unity загрузился и есть игроки
  useEffect(() => {
    console.log('🎮 Проверка готовности:', { isLoaded, playersCount: players.length });
    
    if (isLoaded && players.length > 0) {
      console.log('🚀 Unity готов, отправляем данные');
      
      setTimeout(() => {
        try {
          sendMessage('GameManager', 'SetPlayerCount', players.length);
          console.log('✅ Отправлено количество:', players.length);
          
          players.forEach((player, index) => {
            setTimeout(() => {
              const username = player.user?.username || 'Игрок';
              const userId = player.userId;
              
              console.log(`📨 Отправляем игрока ${index + 1}:`, username);
              sendMessage('GameManager', 'SetPlayerName', username);
              sendMessage('GameManager', 'SetPlayerId', userId);
            }, index * 200);
          });
        } catch (e) {
          console.error('❌ Ошибка отправки:', e);
        }
      }, 1000);
    }
  }, [isLoaded, players, sendMessage]);

  if (loading) {
    return (
      <div className="game-container">
        <div className="game-content">
          <div className="unity-loading">
            <div className="loader"></div>
            <p>Загрузка данных комнаты...</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="game-container">
      <div className="game-header">
        <Button variant="outline" onClick={() => navigate('/rooms')}>
          ← Вернуться к комнатам
        </Button>
        <h1>Игровая комната</h1>
        <div className="players-count">
          Игроков: {players.length}
        </div>
      </div>

      <div className="game-content">
        {!isLoaded && (
          <div className="unity-loading">
            <div className="loader"></div>
            <p>Загрузка игры... {Math.round(loadingProgression * 100)}%</p>
          </div>
        )}
        
        {error && (
          <div style={{ color: 'red', padding: '2rem', textAlign: 'center' }}>
            <p>Ошибка загрузки Unity: {error.message}</p>
          </div>
        )}
        
        <Unity 
          unityProvider={unityProvider} 
          style={{ 
            width: "100%", 
            height: "600px", 
            border: "2px solid #d1fae5", 
            borderRadius: "12px",
            display: isLoaded ? 'block' : 'none'
          }} 
        />
      </div>
    </div>
  );
};

export default Game;