import { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import { notificationsApi } from '../features/notifications/api/notificationsApi';
import { useAuth } from './AuthContext';

const NotificationContext = createContext(null);

const POLL_INTERVAL_MS = 60_000; // 1 min background poll

export function NotificationProvider({ children }) {
  const [unreadCount, setUnreadCount] = useState(0);
  const timerRef = useRef(null);
  const { user } = useAuth();

  const refreshCount = useCallback(async () => {
    try {
      const resp = await notificationsApi.getUnreadCount();
      // API may return { data: n } or just n
      const count = typeof resp === 'number' ? resp : (resp?.data ?? resp ?? 0);
      setUnreadCount(count);
    } catch {
      // noop — unauthenticated or network error
    }
  }, []);

  // Only poll when authenticated
  useEffect(() => {
    if (!user) {
      setUnreadCount(0);
      clearInterval(timerRef.current);
      return;
    }
    refreshCount();
    timerRef.current = setInterval(refreshCount, POLL_INTERVAL_MS);
    return () => clearInterval(timerRef.current);
  }, [user, refreshCount]);

  return (
    <NotificationContext.Provider value={{ unreadCount, refreshCount }}>
      {children}
    </NotificationContext.Provider>
  );
}

export function useNotifications() {
  const ctx = useContext(NotificationContext);
  if (!ctx) throw new Error('useNotifications must be used inside NotificationProvider');
  return ctx;
}
