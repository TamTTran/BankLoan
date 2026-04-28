import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from '../context/AuthContext';
import { ToastProvider } from '../context/ToastContext';
import { NotificationProvider } from '../context/NotificationContext';
import { AppRouter } from './router';
import DebugPanel from '../components/DebugPanel';
import { RouteLogger } from '../components/RouteLogger';

export function App() {
  return (
    <BrowserRouter>
      <RouteLogger />
      <AuthProvider>
        <ToastProvider>
          <NotificationProvider>
            <AppRouter />
          </NotificationProvider>
        </ToastProvider>
      </AuthProvider>
      {/* Dev-only floating action log — renders nothing in production */}
      <DebugPanel />
    </BrowserRouter>
  );
}

