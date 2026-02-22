import React from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import Button from '../components/ui/Button';
import './Game.css';

const Game: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  return (
    <div className="game-container">
      <div className="game-header">
        <Button 
          variant="outline" 
          onClick={() => navigate('/rooms')}
          className="back-button"
        >
          Вернуться к комнатам
        </Button>
        <h1>Игровая комната</h1>
      </div>

      <div className="game-content">
        <div className="unity-placeholder">
          <div className="placeholder-content">
            <div className="unity-logo">🎮</div>
            <h2>Игровое поле</h2>
            <p>Здесь будет интегрировано игровое поле из Unity</p>
          </div>
        </div>
      </div>

      <div className="game-footer">
        <Button variant="outline" onClick={() => navigate('/rooms')}>
          Закрыть
        </Button>
      </div>
    </div>
  );
};

export default Game;