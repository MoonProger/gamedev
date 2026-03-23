import React, { useEffect, useState, useCallback, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Unity, useUnityContext } from "react-unity-webgl";
import { api } from '../services/api';
import Button from '../components/ui/Button';
import Loader from '../components/ui/Loader';
import { useToast } from '../context/ToastContext';
import './Game.css';

interface Player {
  userId: string;
  username: string;
  isReady: boolean;
}

const Game: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [players, setPlayers] = useState<Player[]>([]);
  const [loading, setLoading] = useState(true);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [gameState, setGameState] = useState<any>(null);
  const gameContainerRef = useRef<HTMLDivElement>(null);
  const wsRef = useRef<WebSocket | null>(null);
  const { showToast } = useToast();

  const { 
    unityProvider, 
    isLoaded, 
    loadingProgression, 
    sendMessage,
    unload
  } = useUnityContext({
    loaderUrl: "/Build/Build.loader.js",
    dataUrl: "/Build/Build.data.gz",
    frameworkUrl: "/Build/Build.framework.js.gz",
    codeUrl: "/Build/Build.wasm.gz",
  });

  useEffect(() => {
    if (id) {
      loadRoomData();
    }
  }, [id]);

  const loadRoomData = async () => {
    try {
      const data = await api.getRoom(id!);
      const typedPlayers = data.room.players.map((p: any) => ({
        userId: p.userId,
        username: p.username,
        isReady: p.isReady
      }));
      setPlayers(typedPlayers);
    } catch (err) {
      console.error('Ошибка загрузки комнаты:', err);
      showToast('Не удалось загрузить данные комнаты', 'error');
    } finally {
      setLoading(false);
    }
  };

  // Подключение к WebSocket для получения игровых событий
  useEffect(() => {
    if (!id) return;

    const token = api.getToken();
    if (!token) {
      console.error('No token');
      return;
    }

    const ws = new WebSocket(`ws://localhost:4000/ws?token=${token}`);
    wsRef.current = ws;

    ws.onopen = () => {
      console.log('Game WebSocket connected');
      ws.send(JSON.stringify({
        type: 'room.join',
        payload: { roomId: id }
      }));
    };

    ws.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        console.log('Game WS message:', data);

        switch (data.type) {
          case 'game.state':
            setGameState(data.payload);
            if (isLoaded) {
              sendMessage('GameManager', 'UpdateGameState', JSON.stringify(data.payload));
            }
            break;
          case 'game.started':
            showToast('Игра началась!', 'success');
            break;
          case 'game.dice_rolled':
            if (isLoaded) {
              sendMessage('GameManager', 'OnDiceRolled', data.payload.value);
            }
            break;
          case 'game.move':
            if (isLoaded) {
              sendMessage('GameManager', 'OnPlayerMove', JSON.stringify(data.payload));
            }
            break;
          case 'game.card':
            if (isLoaded) {
              sendMessage('GameManager', 'OnCardPlayed', JSON.stringify(data.payload));
            }
            break;
          case 'game.project':
            if (isLoaded) {
              sendMessage('GameManager', 'OnProjectCompleted', JSON.stringify(data.payload));
            }
            break;
          case 'game.turn_changed':
            if (isLoaded) {
              sendMessage('GameManager', 'OnTurnChanged', data.payload.activePlayerId);
            }
            break;
          case 'error':
            showToast(data.payload.message, 'error');
            break;
        }
      } catch (e) {
        console.error('Error parsing WS message:', e);
      }
    };

    ws.onerror = (error) => {
      console.error('Game WebSocket error:', error);
    };

    ws.onclose = () => {
      console.log('Game WebSocket disconnected');
    };

    return () => {
      if (wsRef.current?.readyState === WebSocket.OPEN) {
        wsRef.current.close();
      }
    };
  }, [id, isLoaded, sendMessage, showToast]);

  // Когда Unity загрузился и есть игроки, отправляем данные
  useEffect(() => {
    if (isLoaded && players.length > 0) {
      console.log('Unity готов, отправляем данные игроков');
      
      setTimeout(() => {
        try {
          sendMessage('GameManager', 'SetPlayerCount', players.length);
          
          players.forEach((player, index) => {
            setTimeout(() => {
              sendMessage('GameManager', 'SetPlayerName', player.username);
              sendMessage('GameManager', 'SetPlayerId', player.userId);
            }, index * 200);
          });
          
          // Если есть сохранённое состояние игры, отправляем его
          if (gameState) {
            setTimeout(() => {
              sendMessage('GameManager', 'UpdateGameState', JSON.stringify(gameState));
            }, players.length * 200 + 500);
          }
        } catch (e) {
          console.error('Ошибка отправки в Unity:', e);
        }
      }, 1000);
    }
  }, [isLoaded, players, sendMessage, gameState]);

  // Выход из игры
  const handleExitGame = async () => {
    try {
      await api.leaveRoom(id!);
      showToast('Вы вышли из игры', 'info');
      if (wsRef.current?.readyState === WebSocket.OPEN) {
        wsRef.current.close();
      }
      await unload();
      navigate('/rooms');
    } catch (err) {
      console.error('Ошибка при выходе:', err);
      showToast('Не удалось выйти из игры', 'error');
      navigate('/rooms');
    }
  };

  // Повторная попытка загрузки Unity
  const handleRetry = () => {
    window.location.reload();
  };

  // Полноэкранный режим
  const toggleFullscreen = useCallback(() => {
    if (!gameContainerRef.current) return;

    if (!isFullscreen) {
      if (gameContainerRef.current.requestFullscreen) {
        gameContainerRef.current.requestFullscreen();
      }
    } else {
      if (document.exitFullscreen) {
        document.exitFullscreen();
      }
    }
  }, [isFullscreen]);

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, []);

  // Прогресс загрузки в процентах
  const loadingPercent = Math.round(loadingProgression * 100);

  if (loading) {
    return <Loader text="Загрузка данных комнаты..." fullPage />;
  }

  return (
    <div className="game-container">
      <div className="game-header">
        <Button variant="outline" onClick={handleExitGame}>
          ← Выйти из игры
        </Button>
        <h1>Игровая комната</h1>
        <Button 
          variant="outline" 
          onClick={toggleFullscreen}
          className="fullscreen-button"
        >
          {isFullscreen ? '⤢ Выйти из полноэкранного' : '⤢ Полноэкранный режим'}
        </Button>
      </div>

      <div className="players-count-header">
        Игроков: {players.length}
      </div>

      <div 
        className="game-content" 
        ref={gameContainerRef}
      >
        {!isLoaded && (
          <div className="unity-loading">
            <div className="progress-bar-container">
              <div 
                className="progress-bar-fill" 
                style={{ width: `${loadingPercent}%` }}
              />
            </div>
            <p>Загрузка игры... {loadingPercent}%</p>
            <div className="loader-spinner-small" />
            <Button 
              variant="outline" 
              onClick={handleRetry}
              style={{ marginTop: '1rem' }}
            >
              Перезагрузить
            </Button>
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