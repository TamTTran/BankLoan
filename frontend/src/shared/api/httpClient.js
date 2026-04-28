import { getToken, clearToken } from '../utils/authStorage';
import { logger } from '../utils/logger';

const API_BASE = '/api';

export async function httpClient(method, path, body = null) {
  const headers = { 'Content-Type': 'application/json' };
  const token = getToken();

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  // ── Log outgoing request ─────────────────────────────────
  logger.apiReq(method, path, body);
  const t0 = Date.now();

  let res;
  try {
    res = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      credentials: 'include',   // BUG-001: Required to send HttpOnly refreshToken cookie
      body: body ? JSON.stringify(body) : null,
    });
  } catch (networkErr) {
    // Network failure (offline, CORS, backend down)
    logger.apiErr(method, path, null, Date.now() - t0, networkErr);
    throw networkErr;
  }

  const ms = Date.now() - t0;

  if (res.status === 401) {
    logger.apiErr(method, path, 401, ms, new Error('Phiên đăng nhập hết hạn'));
    // Only redirect if user was previously authenticated (has a token in memory).
    if (token) {
      // HIGH-004: Attempt server-side token revocation before redirect.
      if (!path.startsWith('/auth/')) {
        try { await fetch(`${API_BASE}/auth/logout`, { method: 'POST', credentials: 'include' }); } catch {}
      }
      clearToken();
      window.location.href = '/';
    }
    throw new Error('Phiên đăng nhập hết hạn');
  }

  // MED-006: Handle rate limiting with user-friendly message
  if (res.status === 429) {
    const err = new Error('Bạn đã thao tác quá nhiều lần. Vui lòng đợi 1 phút rồi thử lại.');
    logger.apiErr(method, path, 429, ms, err);
    throw err;
  }

  const json = await res.json().catch(() => ({}));

  if (!res.ok) {
    const err = new Error(json?.error?.message || json?.message || json?.title || `HTTP ${res.status}`);
    logger.apiErr(method, path, res.status, ms, err);
    throw err;
  }

  // ── Log success response ─────────────────────────────────
  const data = json?.data !== undefined ? json.data : json;
  // Preview: only scalar fields + array length to avoid huge logs
  const preview = Array.isArray(data)
    ? { length: data.length, first: data[0] }
    : typeof data === 'object' && data !== null
      ? Object.fromEntries(Object.entries(data).slice(0, 6))
      : data;
  logger.apiOk(method, path, res.status, ms, preview);

  return data;
}
