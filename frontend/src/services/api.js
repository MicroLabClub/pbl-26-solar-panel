import { mockApi } from './mockData';

const BASE = '/api';
const FORCE_MOCK = import.meta.env.VITE_USE_MOCK === 'true';

// Probe the backend once; if it's down, fall back to mock for the session.
let backendOk = !FORCE_MOCK;
let probed = false;

async function probe() {
  if (probed) return;
  probed = true;
  if (FORCE_MOCK) {
    backendOk = false;
    return;
  }
  try {
    const r = await fetch(`${BASE}/telemetry/summary`, { method: 'GET' });
    backendOk = r.ok;
  } catch {
    backendOk = false;
  }
  if (!backendOk) {
    // eslint-disable-next-line no-console
    console.info('[api] backend unreachable — using mock data');
  }
}

async function call(path, realFn, mockFn) {
  await probe();
  if (!backendOk) return mockFn();
  try {
    return await realFn();
  } catch (e) {
    backendOk = false;
    return mockFn();
  }
}

async function get(path) {
  const r = await fetch(`${BASE}${path}`);
  if (!r.ok) throw new Error(`${r.status} ${path}`);
  return r.json();
}

async function post(path, body) {
  const r = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: body ? { 'Content-Type': 'application/json' } : {},
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!r.ok) throw new Error(`${r.status} ${path}`);
  return r.status === 204 ? null : r.json();
}

export const api = {
  summary: () =>
    call('/telemetry/summary', () => get('/telemetry/summary'), () => mockApi.summary()),
  recent: (limit = 144) =>
    call('/telemetry', () => get(`/telemetry?limit=${limit}`), () => mockApi.recent(limit)),
  history: (days = 2) =>
    call('/telemetry/history', () => get(`/telemetry/history?days=${days}`), () =>
      mockApi.history(days)
    ),
  alerts: (includeAck = false) =>
    call('/alerts', () => get(`/alerts?includeAcknowledged=${includeAck}`), () =>
      mockApi.alerts(includeAck)
    ),
  problems: () =>
    call('/alerts/problems', () => get('/alerts/problems'), () => mockApi.problems()),
  systemChecks: () =>
    call('/telemetry/checks', () => get('/telemetry/checks'), () => mockApi.systemChecks()),
  acknowledge: (id) =>
    call('/alerts/ack', () => post(`/alerts/${id}/acknowledge`), () => mockApi.acknowledge(id)),
  predictions: (hours = 24) =>
    call('/predictions', () => get(`/predictions?hours=${hours}`), () =>
      mockApi.predictions(hours)
    ),

  isMock: () => !backendOk,
};
