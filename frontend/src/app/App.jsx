import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from '../context/AuthContext';
import { ToastProvider } from '../context/ToastContext';
import { NotificationProvider } from '../context/NotificationContext';
import { AppRouter } from './router';

export function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ToastProvider>
          <NotificationProvider>
            <AppRouter />
          </NotificationProvider>
        </ToastProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

