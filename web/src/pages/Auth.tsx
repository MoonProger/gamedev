import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import './Auth.css';
import Button from '../components/ui/Button';
import Input from '../components/ui/Input';

const Auth: React.FC = () => {
  const [isLogin, setIsLogin] = useState(true);
  const [isLoading, setIsLoading] = useState(false);
  const navigate = useNavigate();

  const [formData, setFormData] = useState({
    email: '',
    password: '',
    username: '',
    confirmPassword: '',
  });

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    
    setTimeout(() => {
      setIsLoading(false);
      navigate('/game');
    }, 1500);
  };

  const handleDemoLogin = () => {
    setFormData({
      email: 'demo@example.com',
      password: 'demopassword',
      username: 'Демо-пользователь',
      confirmPassword: 'demopassword',
    });
    setIsLogin(true);
  };

  return (
    <div className="auth">
      <div className="auth-container">
        <div className="auth-header">
          <h1 className="auth-title">
            {isLogin ? 'Вход' : 'Регистрация'}
          </h1>
          <p className="auth-subtitle">
            {isLogin 
              ? 'Войдите в свой аккаунт, чтобы продолжить игру' 
              : 'Создайте аккаунт, чтобы начать свой путь к успеху'}
          </p>
        </div>

        <div className="auth-tabs">
          <button 
            className={`auth-tab ${isLogin ? 'active' : ''}`}
            onClick={() => setIsLogin(true)}
          >
            Вход
          </button>
          <button 
            className={`auth-tab ${!isLogin ? 'active' : ''}`}
            onClick={() => setIsLogin(false)}
          >
            Регистрация
          </button>
        </div>

        <form className="auth-form" onSubmit={handleSubmit}>
          {!isLogin && (
            <Input
              label="Имя пользователя"
              name="username"
              type="text"
              placeholder="Придумайте никнейм"
              value={formData.username}
              onChange={handleInputChange}
              required
            />
          )}

          <Input
            label="Email"
            name="email"
            type="email"
            placeholder="ваш@email.com"
            value={formData.email}
            onChange={handleInputChange}
            required
          />

          <Input
            label="Пароль"
            name="password"
            type="password"
            placeholder="Введите пароль"
            value={formData.password}
            onChange={handleInputChange}
            required
          />

          {!isLogin && (
            <Input
              label="Подтвердите пароль"
              name="confirmPassword"
              type="password"
              placeholder="Повторите пароль"
              value={formData.confirmPassword}
              onChange={handleInputChange}
              required
            />
          )}

          {isLogin && (
            <div className="auth-options">
              <label className="remember-me">
                <input type="checkbox" />
                <span>Запомнить меня</span>
              </label>
              <Link to="/forgot-password" className="forgot-password">
                Забыли пароль?
              </Link>
            </div>
          )}

          <Button 
            type="submit" 
            variant="primary" 
            size="large" 
            fullWidth
            isLoading={isLoading}
          >
            {isLogin ? 'Войти' : 'Создать аккаунт'}
          </Button>

          <div className="demo-login">
            <Button 
              type="button" 
              variant="outline" 
              size="medium" 
              fullWidth
              onClick={handleDemoLogin}
            >
              Попробовать демо-режим
            </Button>
          </div>
        </form>

        <div className="auth-footer">
          <p>
            {isLogin ? 'Еще нет аккаунта?' : 'Уже есть аккаунт?'}
            <button 
              type="button" 
              className="switch-mode"
              onClick={() => setIsLogin(!isLogin)}
            >
              {isLogin ? ' Зарегистрироваться' : ' Войти'}
            </button>
          </p>
        </div>
      </div>
    </div>
  );
};

export default Auth;