import http from 'node:http';
import https from 'node:https';
import { performance } from 'node:perf_hooks';

const baseUrl = process.env.BASE_URL || 'https://localhost';
const targetPath = process.env.TARGET_PATH || '/api/ping';
const method = process.env.METHOD || 'GET';
const rps = Number(process.env.RPS || 1000);
const durationSeconds = Number(process.env.DURATION_SECONDS || 10);
const token = process.env.TOKEN || '';

if (!Number.isFinite(rps) || rps <= 0) throw new Error('RPS must be greater than 0.');
if (!Number.isFinite(durationSeconds) || durationSeconds <= 0) throw new Error('DURATION_SECONDS must be greater than 0.');

const base = new URL(baseUrl);
const transport = base.protocol === 'http:' ? http : https;
const agent = base.protocol === 'http:'
  ? new http.Agent({ keepAlive: true, maxSockets: Math.max(256, rps * 2) })
  : new https.Agent({ keepAlive: true, maxSockets: Math.max(256, rps * 2), rejectUnauthorized: false });

const totalRequests = Math.floor(rps * durationSeconds);
const latencies = [];
const statuses = new Map();
let completed = 0;
let failed = 0;

function recordStatus(status) {
  statuses.set(status, (statuses.get(status) || 0) + 1);
}

function sendOne() {
  const started = performance.now();
  const headers = token ? { Authorization: `Bearer ${token}` } : undefined;

  const req = transport.request({
    protocol: base.protocol,
    hostname: base.hostname,
    port: base.port || (base.protocol === 'http:' ? 80 : 443),
    method,
    path: targetPath,
    headers,
    agent,
    rejectUnauthorized: false,
  }, res => {
    res.resume();
    res.on('end', () => {
      latencies.push(performance.now() - started);
      recordStatus(res.statusCode);
      completed++;
    });
  });

  req.on('error', () => {
    failed++;
    completed++;
  });
  req.end();
}

function percentile(values, p) {
  if (!values.length) return 0;
  const index = Math.min(values.length - 1, Math.floor(values.length * p));
  return values[index];
}

function printSummary() {
  latencies.sort((a, b) => a - b);
  const elapsed = durationSeconds;
  const statusSummary = [...statuses.entries()]
    .sort(([a], [b]) => Number(a) - Number(b))
    .map(([status, count]) => `${status}:${count}`)
    .join(' ');

  console.log(`Target: ${method} ${baseUrl}${targetPath}`);
  console.log(`Requested: ${totalRequests} in ${elapsed}s (${rps} req/s target)`);
  console.log(`Completed: ${completed} Failed transport: ${failed}`);
  console.log(`Status: ${statusSummary || 'none'}`);
  console.log(`Latency ms: p50=${percentile(latencies, 0.50).toFixed(1)} p95=${percentile(latencies, 0.95).toFixed(1)} p99=${percentile(latencies, 0.99).toFixed(1)} max=${(latencies.at(-1) || 0).toFixed(1)}`);

  const rateLimited = statuses.get(429) || 0;
  if (rateLimited > 0) console.log(`Rate limit observed: ${rateLimited} responses with HTTP 429.`);
}

console.log(`Starting load test: ${method} ${baseUrl}${targetPath}, ${rps} req/s for ${durationSeconds}s`);

for (let i = 0; i < totalRequests; i++) {
  setTimeout(sendOne, Math.floor((i * 1000) / rps));
}

const wait = setInterval(() => {
  if (completed >= totalRequests) {
    clearInterval(wait);
    printSummary();
  }
}, 250);
