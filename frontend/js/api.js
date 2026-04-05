/* api.js — LoanFlow Lite API Client */

const API_BASE = 'http://localhost:5000/api';

function getToken() { return localStorage.getItem('lf_token'); }
function setToken(t) { localStorage.setItem('lf_token', t); }
function clearToken() { localStorage.removeItem('lf_token'); localStorage.removeItem('lf_user'); }
function setUser(u) { localStorage.setItem('lf_user', JSON.stringify(u)); }
function getUser() { return JSON.parse(localStorage.getItem('lf_user') || 'null'); }

async function request(method, path, body = null) {
  const headers = { 'Content-Type': 'application/json' };
  const token = getToken();
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : null
  });

  if (res.status === 401) { clearToken(); window.location.reload(); return; }
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(data.message || `HTTP ${res.status}`);
  return data;
}

const api = {
  auth: {
    login: (body) => request('POST', '/auth/login', body)
  },
  customers: {
    list: (params = {}) => request('GET', `/customers?${new URLSearchParams(params)}`),
    get: (id) => request('GET', `/customers/${id}`),
    create: (body) => request('POST', '/customers', body),
    update: (id, body) => request('PUT', `/customers/${id}`, body),
    delete: (id) => request('DELETE', `/customers/${id}`)
  },
  products: {
    list: () => request('GET', '/loan-products'),
    get: (id) => request('GET', `/loan-products/${id}`),
    create: (body) => request('POST', '/loan-products', body),
    update: (id, body) => request('PUT', `/loan-products/${id}`, body),
    preview: (body) => request('POST', '/loan-products/preview-installment', body)
  },
  applications: {
    list: (params = {}) => request('GET', `/applications?${new URLSearchParams(params)}`),
    get: (id) => request('GET', `/applications/${id}`),
    create: (body) => request('POST', '/applications', body),
    submit: (id) => request('POST', `/applications/${id}/submit`),
    decide: (id, body) => request('POST', `/applications/${id}/decide`, body),
    cancel: (id) => request('POST', `/applications/${id}/cancel`)
  },
  loans: {
    list: (params = {}) => request('GET', `/loans?${new URLSearchParams(params)}`),
    get: (id) => request('GET', `/loans/${id}`),
    disburse: (body) => request('POST', '/loans/disburse', body),
    pay: (id, body) => request('POST', `/loans/${id}/payments`, body),
    schedule: (id) => request('GET', `/loans/${id}/schedule`)
  },
  dashboard: {
    summary: () => request('GET', '/dashboard/summary'),
    overdue: () => request('GET', '/dashboard/overdue'),
    updateOverdue: () => request('POST', '/dashboard/update-overdue')
  }
};
