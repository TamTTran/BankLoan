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
    credentials: 'include',   // BUG-001: Required to send HttpOnly refreshToken cookie
    body: body ? JSON.stringify(body) : null,
  });

  if (res.status === 401) {
    // HIGH-004: Attempt server-side token revocation before redirect
    try { await fetch(`${API_BASE}/auth/logout`, { method: 'POST', credentials: 'include' }); } catch {}
    clearToken();
    window.location.href = '/';
    throw new Error('Phiên đăng nhập hết hạn');
  }

  // MED-006: Handle rate limiting with user-friendly message
  if (res.status === 429) {
    throw new Error('Bạn đã thao tác quá nhiều lần. Vui lòng đợi 1 phút rồi thử lại.');
  }

  const json = await res.json().catch(() => ({}));
  
  if (!res.ok) {
    throw new Error(json?.error?.message || json?.message || json?.title || `HTTP ${res.status}`);
  }
  
  // Unwrap { success, data } envelope if present, else return json directly
  return json?.data !== undefined ? json.data : json;
}
