import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import './Auth.css';
import Button from '../components/ui/Button';
import Input from '../components/ui/Input';
import { api } from '../services/api';

const Auth: React.FC = () => {
  const [isLogin, setIsLogin] = useState(true);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
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
    setError(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!isLogin && formData.password !== formData.confirmPassword){
      setError('Пароли не совпадают');
      return;
    }

    if (formData.password.length < 6){
      setError('Пароль должен быть не менее 6 символов');
      return;
    }

    setIsLoading(true);
    setError(null);

    try{
      //проверка на логин
      if (isLogin){
        const data = await api.login(formData.email, formData.password);
        console.log('Успешный вход:', data);
        navigate('/rooms');
      // если регистрация
      } else {
        const data = await api.register(formData.email, formData.username, formData.password);
        console.log('Успешная регистрация', data);
        alert('Регистрация успешна! Теперь вы можете войти');
        setIsLogin(true);
        setFormData({
          email: formData.email,
          password: '',
          username: '',
          confirmPassword: '',
        });
      }
    } catch(err) {
      console.error('Ошибка:', err);
      if (err instanceof Error) {
        if (err.message == 'EMAIL_TAKEN' || err.message.includes('Email already used')){
          setError('Этот email уже зарегистрирован');
        } else if (err.message == 'INVALID_CREDENTIALS' || err.message.includes('Invalid credentials')){
          setError('Неверный email или пароль');
        } else {
          setError(err.message);
        }
      } else {
        setError('Ошибка при подключении к серверу');
      }
    } finally {
      setIsLoading(false);
    }
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
            onClick={() => {
              setIsLogin(true);
              setError(null);
            }}
            type="button"
          >
            Вход
          </button>
          <button 
            className={`auth-tab ${!isLogin ? 'active' : ''}`}
            onClick={() => {
              setIsLogin(false);
              setError(null);
            }}
            type="button"
          >
            Регистрация
          </button>
        </div>

        {error && (
          <div className="error-message" style={{
            backgroundColor: '#fee',
            color: '#c00',
            padding: '12px',
            borderRadius: '8px',
            marginBottom: '20px',
            textAlign: 'center',
            border: '1px solid #fcc'
          }}>
            {error}
          </div>
        )}

        <form className="auth-form" onSubmit={handleSubmit}>
          {!isLogin && (
            <Input
              label="Имя пользователя"
              name="username"
              type="text"
              placeholder="Придумайте никнейм (минимум 2 слова)"
              value={formData.username}
              onChange={handleInputChange}
              required
              disabled = {isLoading}
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
            disabled={isLoading}
          />

          <Input
            label="Пароль"
            name="password"
            type="password"
            placeholder="Введите пароль ( минимум 6 символов)"
            value={formData.password}
            onChange={handleInputChange}
            required
            disabled={isLoading}
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
              disabled={isLoading}
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
        </form>

        <div className="auth-footer">
          <p>
            {isLogin ? 'Еще нет аккаунта?' : 'Уже есть аккаунт?'}
            <button 
              type="button" 
              className="switch-mode"
              onClick={() => {
                setIsLogin(!isLogin)
                setError(null);
              }}
              disabled={isLoading}
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