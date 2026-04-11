import { useEffect } from 'react';

export default function Modal({ id, title, children, actions, size = '', onClose }) {
  useEffect(() => {
    const handler = (e) => { if (e.key === 'Escape' && onClose) onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onClose]);

  return (
    <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget && onClose) onClose(); }}>
      <div className={`modal-box ${size ? `modal-${size}` : ''}`} onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{title}</h3>
          {onClose && <button className="btn-close" onClick={onClose}>✕</button>}
        </div>
        <div className="modal-form">{children}</div>
        {actions && <div className="modal-actions">{actions}</div>}
      </div>
    </div>
  );
}
