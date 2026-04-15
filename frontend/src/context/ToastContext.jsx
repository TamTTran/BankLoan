import { createContext, useContext, useState, useCallback, useEffect, useRef } from 'react';

const ToastContext = createContext(null);

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const timerRef = useRef({});

  const removeToast = useCallback((id) => {
    setToasts(prev => prev.map(t => t.id === id ? { ...t, out: true } : t));
    setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 220);
    delete timerRef.current[id]; // Clean up fired timer ref
  }, []);

  const toast = useCallback((message, type = 'success') => {
    const id = Date.now() + Math.random();
    const icons = { success: '✅', error: '❌', warning: '⚠️', info: 'ℹ️' };
    setToasts(prev => [...prev, { id, message, type, icon: icons[type] || '✅', out: false }]);
    timerRef.current[id] = setTimeout(() => removeToast(id), 3200);
  }, [removeToast]);

  useEffect(() => () => Object.values(timerRef.current).forEach(clearTimeout), []);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div className="toast-container">
        {toasts.map(t => (
          <div key={t.id} className={`toast ${t.type} ${t.out ? 'toast-out' : ''}`}>
            <span>{t.icon}</span>
            <span>{t.message}</span>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export const useToast = () => useContext(ToastContext);
