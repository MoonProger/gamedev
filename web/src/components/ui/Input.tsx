// components/ui/Input.tsx
import React, { useState } from 'react';
import './Input.css';

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  icon?: string;
  helperText?: string;
}

const Input: React.FC<InputProps> = ({
  label,
  error,
  icon,
  helperText,
  className = '',
  type = 'text',
  ...props
}) => {
  const [isFocused, setIsFocused] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const inputType = type === 'password' && showPassword ? 'text' : type;
  const hasIcon = !!icon;

  return (
    <div className={`input-wrapper ${error ? 'has-error' : ''} ${isFocused ? 'focused' : ''}`}>
      {label && (
        <label className="input-label">
          {label}
          {props.required && <span className="required">*</span>}
        </label>
      )}
      
      <div className="input-container">
        {icon && <span className="input-icon">{icon}</span>}
        <input
          type={inputType}
          className={`input ${!hasIcon ? 'no-icon' : ''} ${className}`}
          onFocus={() => setIsFocused(true)}
          onBlur={() => setIsFocused(false)}
          {...props}
        />
        {type === 'password' && (
          <button
            type="button"
            className="password-toggle"
            onClick={() => setShowPassword(!showPassword)}
            aria-label={showPassword ? 'Скрыть пароль' : 'Показать пароль'}
          >
            {showPassword ? '👁️' : '👁️‍🗨️'}
          </button>
        )}
      </div>
      
      {error && <div className="input-error">{error}</div>}
      {helperText && !error && <div className="input-helper">{helperText}</div>}
    </div>
  );
};

export default Input;