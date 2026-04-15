import { getToken, clearToken } from '../utils/authStorage';

const API_BASE = import.meta.env.DEV ? '/api' : '/api';

export async function httpClient(method, path, body = null) {
  const headers = { 'Content-Type': 'application/json' };
  const token = getToken();
  
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : null,
  });

  if (res.status === 401) {
    clearToken();
    window.location.href = '/';
    throw new Error('Phiên đăng nhập hết hạn');
  }

  const json = await res.json().catch(() => ({}));
  
  if (!res.ok) {
    throw new Error(json?.error?.message || json?.message || json?.title || `HTTP ${res.status}`);
  }
  
  // Unwrap { success, data } envelope if present, else return json directly
  return json?.data !== undefined ? json.data : json;
}
