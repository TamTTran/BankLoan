import { getToken, setToken, clearToken, setUser, getUser } from './authStorage';

describe('authStorage', () => {
  beforeEach(() => {
    localStorage.clear();
    clearToken();
  });

  test('should store and retrieve token from memory', () => {
    expect(getToken()).toBeNull();
    setToken('test-token');
    expect(getToken()).toBe('test-token');
  });

  test('should clear token from memory', () => {
    setToken('test-token');
    clearToken();
    expect(getToken()).toBeNull();
  });

  test('should not store token in localStorage', () => {
    setToken('test-token');
    expect(localStorage.getItem('lf_token')).toBeNull();
  });

  test('should store and retrieve user in localStorage', () => {
    const user = { name: 'Test User' };
    setUser(user);
    expect(getUser()).toEqual(user);
    expect(localStorage.getItem('lf_user')).toBeTruthy();
  });

  test('should clear user from localStorage on clearToken', () => {
    setUser({ name: 'Test User' });
    clearToken();
    expect(getUser()).toBeNull();
  });
});
