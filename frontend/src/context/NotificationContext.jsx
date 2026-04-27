import { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import { notificationsApi } from '../features/notifications/api/notificationsApi';
import { useAuth } from './AuthContext';
import * as signalR from '@microsoft/signalr';
import { getToken } from '../shared/utils/authStorage';

const NotificationContext = createContext(null);

export function NotificationProvider({ children }) {
  const [unreadCount, setUnreadCount] = useState(0);
  const connectionRef = useRef(null);
  const { user } = useAuth();

  const refreshCount = useCallback(async () => {
    try {
      const resp = await notificationsApi.getUnreadCount();
      const count = typeof resp === 'number' ? resp : (resp?.data ?? resp ?? 0);
      setUnreadCount(count);
    } catch {
      // noop
    }
  }, []);

  useEffect(() => {
    if (!user) {
      setUnreadCount(0);
      if (connectionRef.current) {
        connectionRef.current.stop();
        connectionRef.current = null;
      }
      return;
    }

    // Initial fetch
    refreshCount();

    // Connect to SignalR
    const token = getToken();
    const API_BASE = import.meta.env.DEV ? '/api' : '/api';
    
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/notification`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build();

    newConnection.on('ReceiveNotification', (notification) => {
      // Incoming notification from backend -> re-fetch unread count
      refreshCount();
    });

    newConnection.start()
      .then(() => console.log('SignalR Connected'))
      .catch(e => console.log('SignalR Connection Error: ', e));

    connectionRef.current = newConnection;

    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop();
      }
    };
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
