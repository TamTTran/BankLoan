import http from 'node:http';
import https from 'node:https';

const baseUrl = process.env.BASE_URL || 'https://localhost';
const username = process.env.SMOKE_USER || 'manager01';
const password = process.env.SMOKE_PASSWORD || 'LoanFlow@2026!';
const deviceName = process.env.SMOKE_DEVICE || 'loanflow-smoke-test';

const base = new URL(baseUrl);
const transport = base.protocol === 'http:' ? http : https;
const agent = base.protocol === 'http:'
  ? new http.Agent({ keepAlive: true })
  : new https.Agent({ keepAlive: true, rejectUnauthorized: false });

function request(method, path, body, token) {
  return new Promise((resolve, reject) => {
    const payload = body ? JSON.stringify(body) : null;
    const headers = { Accept: 'application/json' };
    if (payload) {
      headers['Content-Type'] = 'application/json';
      headers['Content-Length'] = Buffer.byteLength(payload);
    }
    if (token) headers.Authorization = `Bearer ${token}`;

    const req = transport.request({
      protocol: base.protocol,
      hostname: base.hostname,
      port: base.port || (base.protocol === 'http:' ? 80 : 443),
      method,
      path,
      headers,
      agent,
      rejectUnauthorized: false,
    }, res => {
      let text = '';
      res.setEncoding('utf8');
      res.on('data', chunk => { text += chunk; });
      res.on('end', () => {
        let json = null;
        try { json = text ? JSON.parse(text) : null; } catch {}
        resolve({ status: res.statusCode, headers: res.headers, text, json });
      });
    });

    req.on('error', reject);
    if (payload) req.write(payload);
    req.end();
  });
}

async function check(label, method, path, options = {}) {
  const started = Date.now();
  const res = await request(method, path, options.body, options.token);
  const ms = Date.now() - started;
  const expected = options.expect || [200];
  const ok = expected.includes(res.status);
  console.log(`${ok ? 'PASS' : 'FAIL'} ${label} ${method} ${path} -> ${res.status} ${ms}ms`);
  if (!ok) {
    const message = res.json?.message || res.json?.error?.message || res.text.slice(0, 200);
    throw new Error(`${label} expected ${expected.join('/')} but got ${res.status}: ${message}`);
  }
  return res;
}

async function main() {
  console.log(`Smoke target: ${baseUrl}`);

  await check('Gateway health', 'GET', '/health', { expect: [200] });
  await check('Risk SPA route', 'GET', '/risk', { expect: [200] });
  await check('NAPAS SPA route', 'GET', '/napas', { expect: [200] });
  await check('API ping', 'GET', '/api/ping', { expect: [200] });

  const login = await check('Login', 'POST', '/api/auth/login', {
    body: { username, password, deviceName },
    expect: [200],
  });
  const token = login.json?.data?.accessToken;
  if (!token) throw new Error('Login succeeded but accessToken was missing.');

  await check('Risk concentration', 'GET', '/api/risk/concentration', { token });
  await check('Risk ECL', 'GET', '/api/risk/el-summary', { token });
  await check('Capital adequacy', 'GET', '/api/capital-adequacy/latest', { token });
  await check('NAPAS merchant', 'GET', '/api/napas/merchant', { token });
  await check('NAPAS transaction search', 'POST', '/api/napas/transactions/search', {
    token,
    body: { fromDate: '2026-04-24', toDate: '2026-05-01', page: 1, pageSize: 5 },
  });
  await check('NAPAS QR generate', 'POST', '/api/napas/qr/generate', {
    token,
    body: { amount: 100000, description: 'Smoke test', expiryMinutes: 30 },
  });

  console.log('Smoke test completed.');
}

main().catch(error => {
  console.error(`Smoke test failed: ${error.message}`);
  process.exitCode = 1;
});
