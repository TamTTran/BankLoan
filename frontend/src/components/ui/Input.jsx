import React from 'react';
import './Input.css';

export const Input = React.forwardRef(({ label, error, className = '', type = 'text', ...props }, ref) => {
  const isSelect = type === 'select';
  const isTextarea = type === 'textarea';

  return (
    <div className={`input-u-group ${className}`}>
      {label && <label className="input-u-label">{label}</label>}
      
      {isSelect ? (
        <select className={`input-u-field input-u-select ${error ? 'input-u-error' : ''}`} ref={ref} {...props}>
          {props.children}
        </select>
      ) : isTextarea ? (
        <textarea className={`input-u-field input-u-textarea ${error ? 'input-u-error' : ''}`} ref={ref} {...props} />
      ) : (
        <input type={type} className={`input-u-field ${error ? 'input-u-error' : ''}`} ref={ref} {...props} />
      )}
      
      {error && <span className="input-u-error-text">{error}</span>}
    </div>
  );
});

Input.displayName = 'Input';
