import { createContext, useContext, useState, useCallback } from 'react';
import { authApi } from '../features/auth/api/authApi';
import { setToken, setUser, clearToken, getUser } from '../shared/utils/authStorage';
import { buildPermissions } from '../shared/utils/permissions';
import { ROLE_LABELS } from '../utils/format';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUserState] = useState(() => getUser());

  const login = useCallback(async (username, password) => {
    const res = await authApi.login({ username, password });
    setToken(res.accessToken);
    setUser(res);
    setUserState(res);
    return res;
  }, []);

  const logout = useCallback(() => {
    clearToken();
    setUserState(null);
  }, []);

  const roleLabel = ROLE_LABELS[user?.role] || user?.role || '';
  const can = buildPermissions(user?.role);

  return (
    <AuthContext.Provider value={{ user, login, logout, roleLabel, can }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
