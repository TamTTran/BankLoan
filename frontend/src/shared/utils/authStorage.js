let inMemoryToken = null;

export function getToken() {
  return inMemoryToken;
}

export function setToken(token) {
  inMemoryToken = token;
}

export function clearToken() {
  inMemoryToken = null;
  localStorage.removeItem('lf_user');
}

export function setUser(user) {
  localStorage.setItem('lf_user', JSON.stringify(user));
}

export function getUser() {
  return JSON.parse(localStorage.getItem('lf_user') || 'null');
}
