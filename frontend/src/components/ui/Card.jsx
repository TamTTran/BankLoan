import React from 'react';
import './Card.css';

export function Card({ children, className = '', hoverable = false, ...props }) {
  const classes = [
    'card-u',
    hoverable ? 'card-u-hoverable' : '',
    className
  ].filter(Boolean).join(' ');

  return (
    <div className={classes} {...props}>
      {children}
    </div>
  );
}

export function CardHeader({ children, className = '', title, subtitle, action }) {
  return (
    <div className={`card-u-header ${className}`}>
      <div className="card-u-title-group">
        {title && <h3 className="card-u-title">{title}</h3>}
        {subtitle && <p className="card-u-subtitle">{subtitle}</p>}
        {children}
      </div>
      {action && <div className="card-u-action">{action}</div>}
    </div>
  );
}

export function CardBody({ children, className = '' }) {
  return <div className={`card-u-body ${className}`}>{children}</div>;
}
