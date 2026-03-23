import React from 'react';
import './Loader.css';

interface LoaderProps {
  size?: 'small' | 'medium' | 'large';
  text?: string;
  fullPage?: boolean;
}

const Loader: React.FC<LoaderProps> = ({ 
  size = 'medium', 
  text = 'Загрузка...',
  fullPage = false 
}) => {
  const content = (
    <div className={`loader-container loader-${size}`}>
      <div className="loader-spinner" />
      {text && <div className="loader-text">{text}</div>}
    </div>
  );

  if (fullPage) {
    return (
      <div style={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        background: 'rgba(255,255,255,0.9)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 9999
      }}>
        {content}
      </div>
    );
  }

  return content;
};

export default Loader;