import { createContext, useContext, useState, useCallback, useEffect, useRef } from 'react';

const ToastContext = createContext(null);

// Error toasts stay longer so users have time to read them.
const DURATION = { success: 3200, info: 3200, warning: 4500, error: 5500 };

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const timerRef = useRef({});

  const removeToast = useCallback((id) => {
    setToasts(prev => prev.map(t => t.id === id ? { ...t, out: true } : t));
    setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 220);
    delete timerRef.current[id];
  }, []);

  const toast = useCallback((message, type = 'success') => {
    const id = Date.now() + Math.random();
    const icons = { success: '✅', error: '❌', warning: '⚠️', info: 'ℹ️' };
    setToasts(prev => [...prev, { id, message, type, icon: icons[type] || '✅', out: false }]);
    timerRef.current[id] = setTimeout(() => removeToast(id), DURATION[type] ?? 3200);
  }, [removeToast]);

  useEffect(() => () => Object.values(timerRef.current).forEach(clearTimeout), []);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      {/* aria-live so screen readers announce new toasts */}
      <div className="toast-container" role="status" aria-live="polite" aria-atomic="false">
        {toasts.map(t => (
          <div key={t.id} className={`toast ${t.type} ${t.out ? 'toast-out' : ''}`}>
            <span aria-hidden="true">{t.icon}</span>
            <span className="toast-message">{t.message}</span>
            <button
              className="toast-close"
              onClick={() => removeToast(t.id)}
              aria-label="Đóng thông báo"
            >
              ✕
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export const useToast = () => useContext(ToastContext);
