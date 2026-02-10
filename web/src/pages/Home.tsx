import React from 'react';
import { Link } from 'react-router-dom';
import './Home.css';
import Button from '../components/ui/Button';

const Home: React.FC = () => {
  const features = [
    { title: 'Реальные проекты', desc: 'Все карточки основаны на реально существующих федеральных и региональных молодежных инициативах' },
    { title: '8 сфер развития', desc: 'IT, предпринимательство, наука, творчество, волонтерство, медиа, спорт, туризм' },
    { title: 'Свобода выбора', desc: 'Игрок сам определяет, в каких сферах развиваться и какие проекты реализовывать' },
    { title: 'Социальное взаимодействие', desc: 'Возможность совместной реализации проектов и стратегического сотрудничества' },
    { title: 'Симуляция жизни', desc: 'Гранты, путешествия, коллаборации, проекты, накопление опыта' },
    { title: 'Образовательный компонент', desc: 'Игра знакомит с реальными возможностями молодежной политики' },
  ];

  return (
    <div className="home">
      {/* Герой секция */}
      <section className="hero">
        <div className="hero-content">
          <h1 className="hero-title">
            Проект: Молодежь
          </h1>
          <p className="hero-subtitle">
            Онлайн-симулятор жизни активного молодого человека, который через игру знакомит с реальными возможностями самореализации
          </p>
          <div className="hero-buttons">
            <Link to="/auth">
              <Button variant="primary" size="large">
                Начать играть
              </Button>
            </Link>
            <Link to="/game">
              <Button variant="outline" size="large">
                Демо-режим
              </Button>
            </Link>
          </div>
        </div>
        <div className="hero-image">
          <div className="game-preview">
            <div className="preview-card">
              <div className="card-header">Игровой процесс</div>
              <div className="card-content">
                <p>Пошаговая стратегия с элементами образования</p>
                <div className="preview-stats">
                  <div className="stat">30-60 мин</div>
                  <div className="stat">2-4 игрока</div>
                  <div className="stat">12 очков для победы</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Особенности игры */}
      <section className="features">
        <h2 className="section-title">Особенности игры</h2>
        <div className="features-grid">
          {features.map((feature, index) => (
            <div key={index} className="feature-card">
              <h3 className="feature-title">{feature.title}</h3>
              <p className="feature-desc">{feature.desc}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Для кого игра */}
      <section className="audience">
        <h2 className="section-title">Для кого эта игра?</h2>
        <div className="audience-list">
          <div className="audience-item">
            <h3>Молодежь 16-23 лет</h3>
            <p>Старшеклассники, студенты СПО и вузов</p>
          </div>
          <div className="audience-item">
            <h3>Педагоги и наставники</h3>
            <p>Организаторы молодежных мероприятий</p>
          </div>
          <div className="audience-item">
            <h3>Все интересующиеся</h3>
            <p>Кто хочет узнать о возможностях молодежной политики</p>
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="cta">
        <h2 className="cta-title">Готовы начать свой путь к успеху?</h2>
        <p className="cta-subtitle">
          Присоединяйтесь к сотням молодых людей, которые уже открыли для себя новые возможности через игру
        </p>
        <Link to="/auth">
          <Button variant="primary" size="large">
            Начать сейчас
          </Button>
        </Link>
      </section>
    </div>
  );
};

export default Home;