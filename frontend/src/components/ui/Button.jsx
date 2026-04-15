import React from 'react';
import './Button.css'; // Let's define specific button styles here if needed, or rely on global classes. Let's write them locally for modularity.

export function Button({ 
  children, 
  variant = 'primary', 
  size = 'md', 
  className = '', 
  loading = false, 
  block = false,
  ...props 
}) {
  const classes = [
    'btn-u',
    `btn-u-${variant}`,
    `btn-u-size-${size}`,
    block ? 'btn-u-block' : '',
    loading ? 'btn-u-loading' : '',
    className
  ].filter(Boolean).join(' ');

  return (
    <button className={classes} disabled={loading || props.disabled} {...props}>
      {loading && <span className="spinner-u"></span>}
      {children}
    </button>
  );
}
