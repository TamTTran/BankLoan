import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { notificationsApi } from '../api/notificationsApi';

export function NotificationBell() {
  const [count, setCount] = useState(0);
  const navigate = useNavigate();

  const fetchUnread = async () => {
    try {
      const resp = await notificationsApi.getUnreadCount();
      setCount(resp);
    } catch { /* ignore */ }
  };

  useEffect(() => {
    fetchUnread();
    // M2 Fix: Reduced polling from 60s to 5 minutes to prevent API overload at scale
    const interval = setInterval(fetchUnread, 300000); // 5 min
    return () => clearInterval(interval);
  }, []);

  return (
    <button
      onClick={() => navigate('/notifications')}
      className="relative p-2 text-gray-400 hover:text-white focus:outline-none"
    >
      <span className="text-xl">🔔</span>
      {count > 0 && (
        <span className="absolute top-0 right-0 inline-flex items-center justify-center w-4 h-4 text-xs font-bold text-white bg-red-500 rounded-full border-2 border-[#1E1E2D]">
          {count > 99 ? '99+' : count}
        </span>
      )}
    </button>
  );
}
