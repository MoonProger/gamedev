import React, { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate  } from 'react-router-dom';
import { api } from '../../services/api';
import './Header.css';

const Header: React.FC = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [username, setUsername] = useState('');

  useEffect(() => {
    checkAuth();
  }, [location]); 

  const checkAuth = async () => {
    const token = api.getToken();
    if (token) {
      try {
        const data = await api.getMe();
        setUsername(data.user.username);
        setIsLoggedIn(true);
      } catch {
        setIsLoggedIn(false);
      }
    } else {
      setIsLoggedIn(false);
    }
  };

  const handleLogout = () => {
    api.clearToken();
    setIsLoggedIn(false);
    navigate('/');
  };
  
  //для всех
  const navItems = [
    { path: '/', label: 'Главная' },
    { path: '/about', label: 'О проекте' },
  ];

  //игровые комнаты только для авторизованных
  if (isLoggedIn) {
    navItems.push({ path: '/rooms', label: 'Игровые комнаты' });
  }

  return (
    <header className="header">
      <div className="header-container">
        <div className="logo">
          <Link to="/" className="logo-link">
            Проект: Молодежь
          </Link>
        </div>
        
        <nav className="nav">
          <ul className="nav-list">
            {navItems.map((item) => (
              <li key={item.path} className="nav-item">
                <Link 
                  to={item.path} 
                  className={`nav-link ${location.pathname === item.path ? 'active' : ''}`}
                >
                  {item.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>

        <div className="user-actions">
          {isLoggedIn ? (
            <>
              <span className="username">{username}</span>
              <button onClick={handleLogout} className="logout-button">
                Выйти
              </button>
            </>
          ) : (
            <Link to="/auth" className="auth-button">
              Вход / Регистрация
            </Link>
          )}
        </div>
      </div>
    </header>
  );
};

export default Header;