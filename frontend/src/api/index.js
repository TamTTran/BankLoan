/* api/index.js — LoanFlow API Client */

const API_BASE = import.meta.env.DEV ? '/api' : '/api';

function getToken()  { return localStorage.getItem('lf_token'); }
export function setToken(t) { localStorage.setItem('lf_token', t); }
export function clearToken(){ localStorage.removeItem('lf_token'); localStorage.removeItem('lf_user'); }
export function setUser(u)  { localStorage.setItem('lf_user', JSON.stringify(u)); }
export function getUser()   { return JSON.parse(localStorage.getItem('lf_user') || 'null'); }

async function req(method, path, body = null) {
  const headers = { 'Content-Type': 'application/json' };
  const token = getToken();
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : null,
  });

  if (res.status === 401) {
    clearToken();
    window.location.href = '/';
    return;
  }

  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(json?.error?.message || json?.message || json?.title || `HTTP ${res.status}`);
  }
  // Unwrap { success, data } envelope if present
  return json?.data !== undefined ? json.data : json;
}

export const api = {
  auth: {
    login: (body) => req('POST', '/auth/login', body),
  },
  customers: {
    list:   (p = {}) => req('GET', `/customers?${new URLSearchParams(p)}`),
    get:    (id)     => req('GET', `/customers/${id}`),
    create: (body)   => req('POST', '/customers', body),
    update: (id, b)  => req('PATCH', `/customers/${id}`, b),
    checkCredit: (id) => req('POST', `/customers/${id}/check-credit`),
  },
  products: {
    list:   ()         => req('GET', '/loan-products'),
    get:    (id)       => req('GET', `/loan-products/${id}`),
    create: (body)     => req('POST', '/loan-products', body),
    update: (id, body) => req('PATCH', `/loan-products/${id}`, body),
  },
  applications: {
    list:    (p = {}) => req('GET', `/applications?${new URLSearchParams(p)}`),
    get:     (id)     => req('GET', `/applications/${id}`),
    create:  (body)   => req('POST', '/applications', body),
    submit:  (id)     => req('POST', `/applications/${id}/submit`),
    approve: (id)     => req('POST', `/applications/${id}/approve`),
    reject:  (id, b)  => req('POST', `/applications/${id}/reject`, b),
    preview: (body)   => req('POST', '/applications/preview', body),
  },
  loans: {
    list:            (p = {}) => req('GET', `/loans?${new URLSearchParams(p)}`),
    get:             (id)     => req('GET', `/loans/${id}`),
    createFromApp:   (appId)  => req('POST', `/applications/${appId}/create-loan`),
    disburse:        (id, b)  => req('POST', `/loans/${id}/disburse`, b),
    schedule:        (id)     => req('GET', `/loans/${id}/schedule`),
    payments:        (id)     => req('GET', `/loans/${id}/payments`),
    pay:             (id, b)  => req('POST', `/loans/${id}/payments`, b),
    addNote:         (id, b)  => req('POST', `/loans/${id}/collection-notes`, b),
    overdue:         ()       => req('GET', '/overdue'),
  },
  dashboard: {
    summary: () => req('GET', '/dashboard/summary'),
  },
};
