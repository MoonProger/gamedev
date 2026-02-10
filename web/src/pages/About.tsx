import React from 'react';
import './About.css';

const About: React.FC = () => {
  return (
    <div className="about">
      <div className="about-container">
        <h1 className="about-title">О проекте "Молодежь"</h1>
        
        <section className="about-section">
          <h2>Наша миссия</h2>
          <p>
            Создать интерактивную образовательную платформу, которая в игровой форме знакомит 
            молодежь с возможностями самореализации через реальные федеральные и региональные проекты.
          </p>
        </section>

        <section className="about-section">
          <h2>Что такое проект "Молодежь"?</h2>
          <p>
            Это образовательная онлайн-игра, которая моделирует процесс самореализации молодого человека 
            в современном обществе. Игроки знакомятся с реальными возможностями, программами и проектами, 
            доступными для молодежи в России.
          </p>
        </section>

        <section className="about-section">
          <h2>Цели проекта</h2>
          <ul className="goals-list">
            <li>Повысить осведомленность молодежи о существующих возможностях</li>
            <li>Создать увлекательный образовательный инструмент</li>
            <li>Стимулировать активную жизненную позицию</li>
            <li>Развивать навыки стратегического планирования</li>
          </ul>
        </section>

        <section className="about-section">
          <h2>Контакты</h2>
          <ul className="contact-list">
            <li>Email: example@mail.ru</li>
            <li>GitHub: <a href="https://github.com/MoonProger/gamedev">github.com/MoonProger/gamedev</a></li>
          </ul>
        </section>
      </div>
    </div>
  );
};

export default About;