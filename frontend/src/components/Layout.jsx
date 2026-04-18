import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { NotificationBell } from '../features/notifications/components/NotificationBell';

const NAV = [
  { to: '/dashboard', icon: 'DB', label: 'Dashboard', id: 'nav-dashboard' },
  { to: '/customers', icon: 'KH', label: 'Khach hang', id: 'nav-customers' },
  { to: '/products', icon: 'SP', label: 'San pham vay', id: 'nav-products' },
  { to: '/applications', icon: 'HS', label: 'Ho so vay', id: 'nav-applications', badge: true },
  { to: '/loans', icon: 'KV', label: 'Khoan vay', id: 'nav-loans' },
  { to: '/overdue', icon: 'QH', label: 'Qua han', id: 'nav-overdue' },
  { to: '/collections', icon: 'TH', label: 'Thu hoi no', id: 'nav-collections' },
  { to: '/reports', icon: 'BC', label: 'Bao cao', id: 'nav-reports' },
];

export function Layout({ children, pendingCount = 0 }) {
  const { user, logout, roleLabel, can } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  const visible = NAV.filter((n) => {
    if (n.to === '/products' && !can.viewProductsNav) return false;
    if (n.to === '/collections' && !can.viewCollectionQueue) return false;
    if (n.to === '/reports' && !can.viewReports) return false;
    return true;
  });

  return (
    <div className="app-layout">
      <nav className="sidebar">
        <div className="sidebar-brand">
          <div className="brand-icon">LF</div>
          <span className="brand-name">LoanFlow <span>Lite</span></span>
        </div>

        <ul className="nav-menu">
          {visible.map((n) => (
            <li key={n.to}>
              <NavLink
                to={n.to}
                id={n.id}
                className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}
              >
                <i className="nav-icon nav-icon-glyph">{n.icon}</i>
                <span className="nav-label">{n.label}</span>
                {n.badge && pendingCount > 0 && <span className="badge-nav">{pendingCount}</span>}
              </NavLink>
            </li>
          ))}
        </ul>

        <div className="sidebar-footer">
          <div className="avatar">{(user?.fullName || user?.username || 'U')[0].toUpperCase()}</div>
          <div className="sidebar-meta">
            <p className="user-name">{user?.fullName || user?.username}</p>
            <p className="user-role">{roleLabel}</p>
          </div>
          <div className="sidebar-actions">
            <NotificationBell />
            <button className="btn-logout" onClick={handleLogout} title="Dang xuat">
              OUT
            </button>
          </div>
        </div>
      </nav>

      <main className="main-content">{children}</main>
    </div>
  );
}
