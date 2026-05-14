import { mockApi } from './mockData';

const BASE = '/api';
const FORCE_MOCK = import.meta.env.VITE_USE_MOCK === 'true';

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
    console.info('[api] backend unreachable — using mock data');
  }
}

async function call(realFn, mockFn) {
  await probe();
  if (!backendOk) return mockFn();
  try {
    return await realFn();
  } catch {
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

async function put(path, body) {
  const r = await fetch(`${BASE}${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!r.ok) throw new Error(`${r.status} ${path}`);
  return r.status === 204 ? null : r.json();
}

async function del(path) {
  const r = await fetch(`${BASE}${path}`, { method: 'DELETE' });
  if (!r.ok) throw new Error(`${r.status} ${path}`);
}

function withInst(path, installationId) {
  if (!installationId) return path;
  const sep = path.includes('?') ? '&' : '?';
  return `${path}${sep}installationId=${encodeURIComponent(installationId)}`;
}

export const api = {
  summary: (installationId) =>
    call(() => get(withInst('/telemetry/summary', installationId)), () => mockApi.summary()),
  recent: (limit = 144, installationId) =>
    call(() => get(withInst(`/telemetry?limit=${limit}`, installationId)), () => mockApi.recent(limit)),
  series: (hours = 12, installationId) =>
    call(
      () => get(withInst(`/telemetry/series?hours=${hours}`, installationId)),
      () => mockApi.recent(Math.min(2000, hours * 12)).then((r) =>
        [...r].reverse().map((x) => ({
          timestamp: x.timestamp,
          pvPowerWatts: x.data?.pv_input_power ?? 0,
          loadWatts: x.data?.ac_output_active_power ?? 0,
          batterySoc: x.data?.battery_capacity ?? 0,
          batteryVoltage: x.data?.battery_voltage ?? 0,
          batteryFlowWatts:
            ((x.data?.battery_charging_current ?? 0) - (x.data?.battery_discharge_current ?? 0)) *
            (x.data?.battery_voltage ?? 0),
        }))
      )
    ),
  history: (days = 2, installationId) =>
    call(() => get(withInst(`/telemetry/history?days=${days}`, installationId)), () => mockApi.history(days)),
  alerts: (includeAck = false, installationId) =>
    call(
      () => get(withInst(`/alerts?includeAcknowledged=${includeAck}`, installationId)),
      () => mockApi.alerts(includeAck)
    ),
  problems: () => call(() => get('/alerts/problems'), () => mockApi.problems()),
  systemChecks: (installationId) =>
    call(() => get(withInst('/telemetry/checks', installationId)), () => mockApi.systemChecks()),
  acknowledge: (id) =>
    call(() => post(`/alerts/${id}/acknowledge`), () => mockApi.acknowledge(id)),
  predictions: (hours = 24) =>
    call(() => get(`/predictions?hours=${hours}`), () => mockApi.predictions(hours)),

  // Installations CRUD + per-installation analytics (no mock fallback).
  installations: () => get('/installations'),
  installationCreate: (body) => post('/installations', body),
  installationUpdate: (id, body) => put(`/installations/${id}`, body),
  installationDelete: (id) => del(`/installations/${id}`),
  installationForecast: (id, hours = 24) => get(`/installations/${id}/forecast?hours=${hours}`),
  installationPredictions: (id, hours = 24) => get(`/installations/${id}/predictions?hours=${hours}`),
  installationPredictionsToday: (id) => get(`/installations/${id}/predictions/today`),
  installationTrain: (id) => post(`/installations/${id}/train`),

  isMock: () => !backendOk,
};
