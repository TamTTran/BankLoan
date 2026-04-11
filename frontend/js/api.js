/* api.js — LoanFlow Lite API Client (aligned with actual backend) */

// Smart base URL: nginx on port 80 → relative path; local VS dev → localhost:8080
const API_BASE = (() => {
  const { protocol, hostname, port } = window.location;
  if (!port || port === '80' || port === '443') return '/api';
  return `${protocol}//${hostname}:8080/api`;
})();

/* ── Token helpers ── */
function getToken()  { return localStorage.getItem('lf_token'); }
function setToken(t) { localStorage.setItem('lf_token', t); }
function clearToken(){ localStorage.removeItem('lf_token'); localStorage.removeItem('lf_user'); }
function setUser(u)  { localStorage.setItem('lf_user', JSON.stringify(u)); }
function getUser()   { return JSON.parse(localStorage.getItem('lf_user') || 'null'); }

/* ── Core fetch wrapper ── */
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

  // Parse JSON safely
  const json = await res.json().catch(() => ({}));

  if (!res.ok) {
    // Backend error format: { error: { message } } or { message } or string
    const msg = json?.error?.message || json?.message || json?.title || `HTTP ${res.status}`;
    throw new Error(msg);
  }

  // Unwrap { success, data } if present, otherwise return raw
  return json?.data !== undefined ? json.data : json;
}

/* ── API methods aligned with actual controllers ── */
const api = {
  auth: {
    login: (body) => request('POST', '/auth/login', body)
  },

  customers: {
    list:   (params = {}) => request('GET', `/customers?${new URLSearchParams(params)}`),
    get:    (id)          => request('GET', `/customers/${id}`),
    create: (body)        => request('POST', '/customers', body),
    update: (id, body)    => request('PATCH', `/customers/${id}`, body),
  },

  products: {
    list:   ()            => request('GET', '/loan-products'),
    get:    (id)          => request('GET', `/loan-products/${id}`),
    create: (body)        => request('POST', '/loan-products', body),
    update: (id, body)    => request('PATCH', `/loan-products/${id}`, body),
  },

  applications: {
    list:    (params = {}) => request('GET', `/applications?${new URLSearchParams(params)}`),
    get:     (id)          => request('GET', `/applications/${id}`),
    create:  (body)        => request('POST', '/applications', body),
    update:  (id, body)    => request('PATCH', `/applications/${id}`, body),
    submit:  (id)          => request('POST', `/applications/${id}/submit`),
    approve: (id)          => request('POST', `/applications/${id}/approve`),
    reject:  (id, body)    => request('POST', `/applications/${id}/reject`, body),
    preview: (body)        => request('POST', '/applications/preview', body),
    queue:   ()            => request('GET', '/approvals/queue'),
  },

  loans: {
    list:            (params = {}) => request('GET', `/loans?${new URLSearchParams(params)}`),
    get:             (id)          => request('GET', `/loans/${id}`),
    createFromApp:   (applicationId) => request('POST', `/applications/${applicationId}/create-loan`),
    disburse:        (id, body)    => request('POST', `/loans/${id}/disburse`, body),
    schedule:        (id)          => request('GET', `/loans/${id}/schedule`),
    payments:        (id)          => request('GET', `/loans/${id}/payments`),
    pay:             (id, body)    => request('POST', `/loans/${id}/payments`, body),
    addCollectionNote: (id, body)  => request('POST', `/loans/${id}/collection-notes`, body),
    overdue:         ()            => request('GET', '/overdue'),
  },

  dashboard: {
    summary:        () => request('GET', '/dashboard/summary'),
    overdueBuckets: () => request('GET', '/reports/overdue-buckets'),
  }
};
