import { useNavigate } from 'react-router-dom';
import { useNotifications } from '../../../context/NotificationContext';

export function NotificationBell() {
  const { unreadCount } = useNotifications();
  const navigate = useNavigate();

  return (
    <button
      onClick={() => navigate('/notifications')}
      className="notification-bell"
      title="Notifications"
    >
      <span className="notification-bell-icon">🔔</span>
      {unreadCount > 0 && (
        <span className="notification-bell-count">
          {unreadCount > 99 ? '99+' : unreadCount}
        </span>
      )}
    </button>
  );
}
