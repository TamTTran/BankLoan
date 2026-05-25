import { lazy, Suspense, useState } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { Layout } from '../components/Layout';
import Login        from '../pages/Login';
import Dashboard    from '../pages/Dashboard';
import Customers    from '../pages/Customers';
import LoanProducts from '../pages/LoanProducts';
import Applications from '../pages/Applications';
import Loans        from '../pages/Loans';
import Overdue      from '../pages/Overdue';
import Collections  from '../pages/Collections';
import Notifications from '../pages/Notifications';
import Approvals    from '../pages/Approvals';
import Profile from '../pages/Profile';

const SystemOps = lazy(() => import('../pages/SystemOps'));
const AmlDashboard = lazy(() => import('../pages/AmlDashboard'));
const RiskDashboard = lazy(() => import('../pages/RiskDashboard'));
const RegulatoryReports = lazy(() => import('../pages/RegulatoryReports'));
const InterbankSync = lazy(() => import('../pages/InterbankSync'));
const Napas = lazy(() => import('../pages/Napas'));
const Reports = lazy(() => import('../pages/Reports'));
const EWalletFlowMap = lazy(() => import('../pages/EWalletFlowMap'));

function PrivateRoute({ children }) {
  const { user } = useAuth();
  return user ? children : <Navigate to="/" replace />;
}

function PermissionRoute({ allowed, children }) {
  return allowed ? children : <Navigate to="/dashboard" replace />;
}

function AppShell() {
  const { can } = useAuth();
  const [badgeCounts, setBadgeCounts] = useState({ applications: 0, approvals: 0 });
  return (
    <Layout badgeCounts={badgeCounts}>
      <Suspense fallback={<div className="page"><p style={{ color: 'var(--text-muted)' }}>Loading module...</p></div>}>
        <Routes>
          <Route path="/dashboard"    element={<Dashboard onBadgeCounts={setBadgeCounts} />} />
          <Route path="/customers"    element={<Customers />} />
          <Route path="/products"     element={<PermissionRoute allowed={can.viewProductsNav}><LoanProducts /></PermissionRoute>} />
          <Route path="/applications" element={<Applications />} />
          <Route path="/loans"        element={<Loans />} />
          <Route path="/overdue"      element={<Overdue />} />
          <Route path="/collections"  element={<PermissionRoute allowed={can.viewCollectionQueue}><Collections /></PermissionRoute>} />
          <Route path="/reports"      element={<PermissionRoute allowed={can.viewReports}><Reports /></PermissionRoute>} />
          <Route path="/notifications" element={<Notifications />} />
          <Route path="/approvals"    element={<PermissionRoute allowed={can.viewApprovalsNav}><Approvals /></PermissionRoute>} />
          <Route path="/system-ops"   element={<PermissionRoute allowed={can.viewSystemOpsNav}><SystemOps /></PermissionRoute>} />
          <Route path="/aml"          element={<PermissionRoute allowed={can.viewAmlNav}><AmlDashboard /></PermissionRoute>} />
          <Route path="/risk"         element={<PermissionRoute allowed={can.viewRiskNav}><RiskDashboard /></PermissionRoute>} />
          <Route path="/regulatory"   element={<PermissionRoute allowed={can.viewRegulatoryNav}><RegulatoryReports /></PermissionRoute>} />
          <Route path="/napas"        element={<PermissionRoute allowed={can.viewNapasNav}><Napas /></PermissionRoute>} />
          <Route path="/interbank-sync" element={<PermissionRoute allowed={can.viewInterbankNav}><InterbankSync /></PermissionRoute>} />
          <Route path="/ewallet-flow" element={<EWalletFlowMap />} />
          <Route path="/profile"      element={<Profile />} />
          <Route path="*"             element={<Navigate to="/dashboard" replace />} />
        </Routes>
      </Suspense>
    </Layout>
  );
}

export function AppRouter() {
  const { user } = useAuth();
  return (
    <Routes>
      <Route path="/" element={user ? <Navigate to="/dashboard" replace /> : <Login />} />
      <Route path="/*" element={<PrivateRoute><AppShell /></PrivateRoute>} />
    </Routes>
  );
}
