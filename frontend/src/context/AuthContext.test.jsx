import { render, screen, waitFor } from '@testing-library/react';
import { AuthProvider, useAuth } from './AuthContext';
import { authApi } from '../features/auth/api/authApi';
import { setUser, clearToken, getToken } from '../shared/utils/authStorage';
import React from 'react';

import { vi } from 'vitest';

// Mock authApi
vi.mock('../features/auth/api/authApi', () => ({
  authApi: {
    refresh: vi.fn(),
    login: vi.fn(),
  }
}));

const TestComponent = () => {
  const { user, login, logout } = useAuth();
  return (
    <div>
      <div data-testid="user-role">{user ? user.role : 'none'}</div>
      <button onClick={() => login('test', 'password')}>Login</button>
      <button onClick={logout}>Logout</button>
    </div>
  );
};

describe('AuthContext', () => {
  beforeEach(() => {
    localStorage.clear();
    clearToken();
    vi.clearAllMocks();
  });

  test('should initialize with null user if not in storage', async () => {
    render(
      <AuthProvider>
        <TestComponent />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('user-role').textContent).toBe('none');
    });
  });

  test('should attempt silent refresh if user in storage but no token', async () => {
    setUser({ role: 'Admin', username: 'admin' });
    authApi.refresh.mockResolvedValueOnce({ accessToken: 'new-token', role: 'Admin' });

    render(
      <AuthProvider>
        <TestComponent />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(authApi.refresh).toHaveBeenCalledTimes(1);
      expect(screen.getByTestId('user-role').textContent).toBe('Admin');
      expect(getToken()).toBe('new-token');
    });
  });

  test('should clear user if silent refresh fails', async () => {
    setUser({ role: 'Admin', username: 'admin' });
    authApi.refresh.mockRejectedValueOnce(new Error('Refresh failed'));

    render(
      <AuthProvider>
        <TestComponent />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(authApi.refresh).toHaveBeenCalledTimes(1);
      expect(screen.getByTestId('user-role').textContent).toBe('none');
      expect(getToken()).toBeNull();
    });
  });
});
