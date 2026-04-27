import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useT } from '../i18n/useT';
import { NotificationBell } from '../features/notifications/components/NotificationBell';
import { LanguageSwitcher } from './LanguageSwitcher';

export function Layout({ children, pendingCount = 0 }) {
  const { user, logout, roleLabel, can } = useAuth();
  const { t } = useT();
  const navigate = useNavigate();

  const NAV = [
    { to: '/dashboard',    icon: 'DB', label: t('nav.dashboard'),    id: 'nav-dashboard' },
    { to: '/customers',    icon: 'KH', label: t('nav.customers'),    id: 'nav-customers' },
    { to: '/products',     icon: 'SP', label: t('nav.products'),     id: 'nav-products',     gated: !can.viewProductsNav },
    { to: '/applications', icon: 'HS', label: t('nav.applications'), id: 'nav-applications', badge: true },
    { to: '/loans',        icon: 'KV', label: t('nav.loans'),        id: 'nav-loans' },
    { to: '/overdue',      icon: 'QH', label: t('nav.overdue'),      id: 'nav-overdue' },
    { to: '/collections',  icon: 'TH', label: t('nav.collections'),  id: 'nav-collections',  gated: !can.viewCollectionQueue },
    { to: '/reports',      icon: 'BC', label: t('nav.reports'),      id: 'nav-reports',      gated: !can.viewReports },
    { to: '/aml',          icon: '🛡️', label: 'AML/KYC',            id: 'nav-aml' },
    { to: '/risk',         icon: '⚖️', label: 'Risk & Capital',     id: 'nav-risk' },
    { to: '/regulatory',   icon: '📜', label: 'Regulatory',         id: 'nav-reg' },
    { to: '/approvals',    icon: '✅', label: t('nav.approvals'),    id: 'nav-approvals',    badge: true, gated: !can.viewApprovalsNav },
    { to: '/system-ops',   icon: '⚙️', label: t('nav.system'),      id: 'nav-ops',          gated: !can.viewSystemOpsNav },
  ];

  const visible = NAV.filter(n => !n.gated);

  const handleLogout = () => {
    logout();
    navigate('/');
  };

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
          <NavLink to="/profile" className="avatar">{(user?.fullName || user?.username || 'U')[0].toUpperCase()}</NavLink>
          <div className="sidebar-meta">
            <p className="user-name"><NavLink to="/profile" style={{ color: 'inherit', textDecoration: 'none' }}>{user?.fullName || user?.username}</NavLink></p>
            <p className="user-role">{roleLabel}</p>
          </div>
          <div className="sidebar-actions">
            <LanguageSwitcher />
            <NotificationBell />
            <button className="btn-logout" onClick={handleLogout} title={t('common.logout')}>
              OUT
            </button>
          </div>
        </div>
      </nav>

      <main className="main-content">{children}</main>
    </div>
  );
}
