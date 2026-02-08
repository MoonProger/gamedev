import React from 'react';
import { Link, useLocation } from 'react-router-dom';
import './Header.css';

const Header: React.FC = () => {
  const location = useLocation();
  
  const navItems = [
    { path: '/', label: 'Главная' },
    { path: '/game', label: 'Играть' },
    { path: '/about', label: 'О проекте' },
  ];

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
          <Link to="/auth" className="auth-button">
            Войти
          </Link>
        </div>
      </div>
    </header>
  );
};

export default Header;