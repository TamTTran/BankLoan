import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { authApi } from '../features/auth/api/authApi';
import { httpClient } from '../shared/api/httpClient';
import { setToken, setUser, clearToken, getUser, getToken } from '../shared/utils/authStorage';
import { buildPermissions } from '../shared/utils/permissions';
import { ROLE_LABELS } from '../utils/format';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUserState] = useState(() => getUser());
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const initAuth = async () => {
      const savedUser = getUser();
      if (savedUser && !getToken()) {
        try {
          const res = await authApi.refresh();
          setToken(res.accessToken);
          setUser(res);
          setUserState(res);
        } catch (error) {
          clearToken();
          setUserState(null);
        }
      }
      setLoading(false);
    };
    initAuth();
  }, []);

  const login = useCallback(async (username, password) => {
    const res = await authApi.login({ username, password });
    if (res.requiresMfa) {
      return res;
    }
    setToken(res.accessToken);
    setUser(res);
    setUserState(res);
    return res;
  }, []);

  const verifyMfa = useCallback(async (mfaToken, totpCode) => {
    const res = await httpClient('POST', '/auth/verify-mfa', { mfaToken, totpCode });
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
  const rolePermissions = buildPermissions(user?.role);
  const apiPermissions = user?.permissions
    ? user.permissions.reduce((acc, p) => ({ ...acc, [p]: true }), {})
    : {};
  const can = { ...rolePermissions, ...apiPermissions };

  if (loading) return null;

  return (
    <AuthContext.Provider value={{ user, login, verifyMfa, logout, roleLabel, can }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
