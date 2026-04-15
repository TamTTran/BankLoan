export function getToken() {
  return localStorage.getItem('lf_token');
}

export function setToken(token) {
  localStorage.setItem('lf_token', token);
}

export function clearToken() {
  localStorage.removeItem('lf_token');
  localStorage.removeItem('lf_user');
}

export function setUser(user) {
  localStorage.setItem('lf_user', JSON.stringify(user));
}

export function getUser() {
  return JSON.parse(localStorage.getItem('lf_user') || 'null');
}
