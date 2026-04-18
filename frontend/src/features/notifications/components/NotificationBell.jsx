import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { notificationsApi } from '../api/notificationsApi';

export function NotificationBell() {
  const [count, setCount] = useState(0);
  const navigate = useNavigate();

  const fetchUnread = async () => {
    try {
      const resp = await notificationsApi.getUnreadCount();
      setCount(resp);
    } catch {
      // noop
    }
  };

  useEffect(() => {
    fetchUnread();
    const interval = setInterval(fetchUnread, 300000);
    return () => clearInterval(interval);
  }, []);

  return (
    <button
      onClick={() => navigate('/notifications')}
      className="notification-bell"
      title="Notifications"
    >
      <span className="notification-bell-icon">N</span>
      {count > 0 && (
        <span className="notification-bell-count">
          {count > 99 ? '99+' : count}
        </span>
      )}
    </button>
  );
}
