import { createContext, useContext, useState, useCallback } from 'react';
import { api, setToken, setUser, clearToken, getUser } from '../api';
import { ROLE_LABELS } from '../utils/format';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUserState] = useState(() => getUser());

  const login = useCallback(async (username, password) => {
    const res = await api.auth.login({ username, password });
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
  const isRole = (...roles) => roles.includes(user?.role);
  const can = {
    createCustomer:     isRole('Admin', 'LoanOfficer'),
    createApplication:  isRole('Admin', 'LoanOfficer'),
    submitApplication:  isRole('Admin', 'LoanOfficer'),
    approveApplication: isRole('Admin', 'Approver'),
    createLoan:         isRole('Admin', 'LoanOfficer'),
    disburse:           isRole('Admin', 'LoanOfficer'),
    recordPayment:      isRole('Admin', 'LoanOfficer'),
    addCollectionNote:  isRole('Admin', 'Collector'),
    manageProducts:     isRole('Admin'),
    viewOverdue:        isRole('Admin', 'Collector', 'Manager', 'LoanOfficer'),
  };

  return (
    <AuthContext.Provider value={{ user, login, logout, roleLabel, can, isRole }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
