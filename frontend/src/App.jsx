import { useState } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuth } from './context/AuthContext';
import { Layout } from './components/Layout';
import Login        from './pages/Login';
import Dashboard    from './pages/Dashboard';
import Customers    from './pages/Customers';
import LoanProducts from './pages/LoanProducts';
import Applications from './pages/Applications';
import Loans        from './pages/Loans';
import Overdue      from './pages/Overdue';

function PrivateRoute({ children }) {
  const { user } = useAuth();
  return user ? children : <Navigate to="/" replace />;
}

function AppShell() {
  const [pendingCount, setPendingCount] = useState(0);
  return (
    <Layout pendingCount={pendingCount}>
      <Routes>
        <Route path="/dashboard"    element={<Dashboard onPendingCount={setPendingCount} />} />
        <Route path="/customers"    element={<Customers />} />
        <Route path="/products"     element={<LoanProducts />} />
        <Route path="/applications" element={<Applications />} />
        <Route path="/loans"        element={<Loans />} />
        <Route path="/overdue"      element={<Overdue />} />
        <Route path="*"             element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </Layout>
  );
}

export default function App() {
  const { user } = useAuth();
  return (
    <Routes>
      <Route path="/" element={user ? <Navigate to="/dashboard" replace /> : <Login />} />
      <Route path="/*" element={<PrivateRoute><AppShell /></PrivateRoute>} />
    </Routes>
  );
}
