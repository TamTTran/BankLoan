import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

const NAV = [
  { to: '/dashboard',    icon: '📊', label: 'Dashboard',         id: 'nav-dashboard'    },
  { to: '/customers',   icon: '👥', label: 'Khách hàng',        id: 'nav-customers'   },
  { to: '/products',    icon: '📦', label: 'Sản phẩm vay',      id: 'nav-products'    },
  { to: '/applications',icon: '📋', label: 'Hồ sơ vay',         id: 'nav-applications',badge: true },
  { to: '/loans',       icon: '💰', label: 'Khoản vay',         id: 'nav-loans'       },
  { to: '/overdue',     icon: '⚠️', label: 'Quá hạn',           id: 'nav-overdue'     },
];

export function Layout({ children, pendingCount = 0 }) {
  const { user, logout, roleLabel, can } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => { logout(); navigate('/'); };

  const visible = NAV.filter(n => {
    if (n.to === '/products' && !can.viewProductsNav) return false;
    return true;
  });

  return (
    <div className="app-layout">
      <nav className="sidebar">
        <div className="sidebar-brand">
          <div className="brand-icon">💳</div>
          <span className="brand-name">LoanFlow <span>Lite</span></span>
        </div>

        <ul className="nav-menu">
          {visible.map(n => (
            <li key={n.to}>
              <NavLink
                to={n.to}
                id={n.id}
                className={({ isActive }) => `nav-item${isActive ? ' active' : ''}`}
              >
                <i className="nav-icon">{n.icon}</i>
                <span>{n.label}</span>
                {n.badge && pendingCount > 0 && (
                  <span className="badge-nav">{pendingCount}</span>
                )}
              </NavLink>
            </li>
          ))}
        </ul>

        <div className="sidebar-footer">
          <div className="avatar">{(user?.fullName || user?.username || 'U')[0].toUpperCase()}</div>
          <div>
            <p className="user-name">{user?.fullName || user?.username}</p>
            <p className="user-role">{roleLabel}</p>
          </div>
          <button className="btn-logout" onClick={handleLogout} title="Đăng xuất">⏻</button>
        </div>
      </nav>

      <main className="main-content">
        {children}
      </main>
    </div>
  );
}
