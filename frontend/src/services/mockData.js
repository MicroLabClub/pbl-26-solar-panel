// Synthetic data so the dashboard runs without the backend.
// Generates rolling readings, alerts, multi-day history & forecast,
// plus system health checks. Shapes mirror the API.

function rng(seed) {
  let s = seed;
  return () => {
    s = (s * 9301 + 49297) % 233280;
    return s / 233280;
  };
}

function makeReading(ts, solar, rand) {
  const pvPower = Math.max(0, solar * 1850 + (rand() - 0.5) * 80);
  const load = 220 + rand() * 380;
  return {
    id: `mock-${ts.getTime()}`,
    timestamp: ts.toISOString(),
    source: 'mpp-solar',
    data: {
      _command: 'QPIGS',
      _command_description: 'General Status Parameters inquiry',
      ac_input_voltage: 240 + (rand() - 0.5) * 2,
      ac_input_frequency: 50.0,
      ac_output_voltage: 230 + (rand() - 0.5) * 2,
      ac_output_frequency: 49.9,
      ac_output_apparent_power: load + 20,
      ac_output_active_power: load,
      ac_output_load: (load / 3000) * 100,
      bus_voltage: 430 + rand() * 2,
      battery_voltage: 26.5 + solar * 1.5,
      battery_charging_current: solar * 30,
      battery_capacity: Math.min(100, 55 + solar * 45),
      inverter_heat_sink_temperature: 35 + solar * 18,
      pv_input_current_for_battery: solar * 6,
      pv_input_voltage: solar > 0.05 ? 220 + solar * 30 : 0,
      battery_voltage_from_scc: solar > 0.05 ? 27 + solar : 0,
      battery_discharge_current: solar < 0.1 ? 4 : 0,
      is_load_on: 1,
      is_charging_on: solar > 0.1 ? 1 : 0,
      is_scc_charging_on: solar > 0.1 ? 1 : 0,
      is_ac_charging_on: 0,
      pv_input_power: pvPower,
      is_switched_on: 1,
      is_sbu_priority_version_added: 0,
      is_configuration_changed: 0,
      is_scc_firmware_updated: 0,
      is_battery_voltage_to_steady_while_charging: 0,
      rsv1: 0,
      rsv2: 0,
      is_charging_to_float: 0,
      is_reserved: 0,
    },
  };
}

function buildReadings(count = 144) {
  const rand = rng(Date.now() % 100000);
  const now = new Date();
  const out = [];
  for (let i = count - 1; i >= 0; i--) {
    const ts = new Date(now.getTime() - i * 5 * 60_000);
    const hour = ts.getHours() + ts.getMinutes() / 60;
    const solar = Math.max(0, Math.sin(((hour - 6) / 12) * Math.PI));
    out.push(makeReading(ts, solar, rand));
  }
  return out.reverse();
}

// Hourly historical aggregates spanning `days` back from now.
// Slight day-to-day variance simulates weather.
function buildHistoryHourly(days = 2) {
  const rand = rng(20260430);
  const now = new Date();
  now.setMinutes(0, 0, 0);
  const totalHours = days * 24;
  const out = [];
  for (let i = totalHours; i >= 1; i--) {
    const t = new Date(now.getTime() - i * 3600_000);
    const dayIdx = Math.floor(i / 24);
    const cloudFactor = 0.65 + rand() * 0.35 - dayIdx * 0.05;
    const localHour = t.getHours();
    const solar = Math.max(0, Math.sin(((localHour - 6) / 12) * Math.PI));
    const powerW = solar * 1900 * cloudFactor;
    out.push({
      hour: t.toISOString(),
      producedPowerWatts: Math.round(powerW),
      producedEnergyWh: Math.round(powerW),
      loadWatts: Math.round(280 + rand() * 320),
    });
  }
  return out;
}

function buildAlerts() {
  const now = Date.now();
  return [
    {
      id: 'mock-alert-1',
      timestamp: new Date(now - 37 * 60_000).toISOString(),
      severity: 'Warning',
      code: 'TEMP_HIGH',
      title: 'Inverter heat sink temperature elevated',
      message: 'Heat sink reached 52°C. Verify ventilation and clear obstructions.',
      acknowledged: false,
    },
    {
      id: 'mock-alert-2',
      timestamp: new Date(now - 3 * 3600_000).toISOString(),
      severity: 'Info',
      code: 'PV_LOW_OUTPUT',
      title: 'PV output below expected',
      message: 'Midday output 18% below 7-day rolling average. Check for shading or soiling.',
      acknowledged: false,
    },
    {
      id: 'mock-alert-3',
      timestamp: new Date(now - 9 * 3600_000).toISOString(),
      severity: 'Critical',
      code: 'BATTERY_LOW',
      title: 'Battery capacity dropped below 20%',
      message: 'SOC reached 18% overnight. Consider load shedding or grid charge fallback.',
      acknowledged: false,
    },
  ];
}

// Problem intervals — each entry has start and (if resolved) end timestamps.
function buildProblems() {
  const now = Date.now();
  const h = 3600_000;
  return [
    {
      id: 'prob-1',
      code: 'TEMP_HIGH',
      severity: 'Warning',
      title: 'Inverter heat sink temperature elevated',
      message: 'Heat sink temperature exceeded 50°C threshold for 42 minutes.',
      startedAt: new Date(now - 1.2 * h).toISOString(),
      endedAt: null,
      occurrences: 1,
    },
    {
      id: 'prob-2',
      code: 'PV_LOW_OUTPUT',
      severity: 'Warning',
      title: 'PV underperforming for time of day',
      message: 'PV power 18% below rolling average between 12:10–14:55.',
      startedAt: new Date(now - 4 * h).toISOString(),
      endedAt: new Date(now - 1 * h).toISOString(),
      occurrences: 1,
    },
    {
      id: 'prob-3',
      code: 'BATTERY_LOW',
      severity: 'Critical',
      title: 'Battery capacity dropped below 20%',
      message: 'State of charge reached 18% overnight before sunrise recovery.',
      startedAt: new Date(now - 11 * h).toISOString(),
      endedAt: new Date(now - 7 * h).toISOString(),
      occurrences: 1,
    },
    {
      id: 'prob-4',
      code: 'GRID_UNDERVOLT',
      severity: 'Warning',
      title: 'Grid undervoltage events',
      message: 'AC input dipped below 200 V briefly on three separate occasions.',
      startedAt: new Date(now - 26 * h).toISOString(),
      endedAt: new Date(now - 25.5 * h).toISOString(),
      occurrences: 3,
    },
    {
      id: 'prob-5',
      code: 'COMM_LOSS',
      severity: 'Info',
      title: 'Telemetry gap',
      message: 'No data received from Raspberry Pi for 6 minutes.',
      startedAt: new Date(now - 30 * h).toISOString(),
      endedAt: new Date(now - 30 * h + 6 * 60_000).toISOString(),
      occurrences: 1,
    },
  ];
}

// Subsystem health checks — what's running well vs. degraded.
function buildSystemChecks(latest) {
  const d = latest.data;
  const checks = [
    {
      key: 'pv',
      label: 'PV Array',
      status: d.pv_input_power > 30 || d.pv_input_voltage < 50
        ? 'ok' : 'warn',
      detail: d.pv_input_power > 30
        ? `${Math.round(d.pv_input_power)} W from ${d.pv_input_voltage.toFixed(0)} V input`
        : 'No production — expected if night / heavy cloud',
    },
    {
      key: 'inverter',
      label: 'Inverter',
      status: d.is_switched_on && d.inverter_heat_sink_temperature < 60 ? 'ok'
        : d.inverter_heat_sink_temperature >= 65 ? 'critical' : 'warn',
      detail: `Switched ${d.is_switched_on ? 'ON' : 'OFF'} · ${Math.round(d.inverter_heat_sink_temperature)}°C heat sink`,
    },
    {
      key: 'battery',
      label: 'Battery Bank',
      status: d.battery_capacity >= 40 ? 'ok'
        : d.battery_capacity >= 20 ? 'warn' : 'critical',
      detail: `${Math.round(d.battery_capacity)}% SOC · ${d.battery_voltage.toFixed(2)} V`,
    },
    {
      key: 'charging',
      label: 'Solar Charge Controller',
      status: d.is_scc_charging_on || d.pv_input_power < 30 ? 'ok' : 'warn',
      detail: d.is_scc_charging_on
        ? `Charging at ${d.battery_charging_current.toFixed(1)} A`
        : 'Idle (no PV input)',
    },
    {
      key: 'load',
      label: 'Output Load',
      status: d.ac_output_load < 80 ? 'ok' : d.ac_output_load < 95 ? 'warn' : 'critical',
      detail: `${Math.round(d.ac_output_active_power)} W (${d.ac_output_load.toFixed(0)}% of capacity)`,
    },
    {
      key: 'grid',
      label: 'Grid Connection',
      status: d.ac_input_voltage > 210 && d.ac_input_voltage < 250 ? 'ok' : 'warn',
      detail: `${d.ac_input_voltage.toFixed(1)} V @ ${d.ac_input_frequency.toFixed(1)} Hz`,
    },
    {
      key: 'comm',
      label: 'Telemetry Link',
      status: 'ok',
      detail: 'Last sample <1 min ago',
    },
  ];
  return checks;
}

function buildPredictions(hours = 24) {
  const start = new Date();
  start.setMinutes(0, 0, 0);
  const peak = 1900;
  const hourly = [];
  let totalWh = 0;
  const rand = rng(start.getDate());
  for (let i = 0; i < hours; i++) {
    const t = new Date(start.getTime() + i * 3600_000);
    const dayOffset = Math.floor(i / 24);
    const cloudFactor = 0.7 + rand() * 0.3 - dayOffset * 0.04;
    const localHour = t.getHours();
    const solar = Math.max(0, Math.sin(((localHour - 6) / 12) * Math.PI));
    const power = solar * peak * Math.max(0.4, cloudFactor);
    totalWh += power;
    hourly.push({
      hour: t.toISOString(),
      predictedPowerWatts: Math.round(power * 10) / 10,
      predictedEnergyWh: Math.round(power * 10) / 10,
      confidence: solar > 0 ? Math.max(0.5, 0.9 - dayOffset * 0.1) : 0.95,
    });
  }
  return {
    generatedAt: new Date().toISOString(),
    method: 'mock · clear-sky sinusoid + cloud variance',
    totalPredictedEnergyKwh: Math.round((totalWh / 1000) * 100) / 100,
    hourly,
  };
}

function buildSummary(latest, readings) {
  const today = new Date().toDateString();
  const todayWh =
    readings
      .filter((r) => new Date(r.timestamp).toDateString() === today)
      .reduce((sum, r) => sum + r.data.pv_input_power, 0) * (5 / 60);

  const d = latest.data;
  return {
    timestamp: latest.timestamp,
    currentPvPower: d.pv_input_power,
    currentLoad: d.ac_output_active_power,
    batterySoc: d.battery_capacity,
    batteryVoltage: d.battery_voltage,
    heatSinkC: d.inverter_heat_sink_temperature,
    gridVoltage: d.ac_input_voltage,
    isCharging: d.is_charging_on === 1,
    isLoadOn: d.is_load_on === 1,
    todayEnergyKwh: Math.round((todayWh / 1000) * 100) / 100,
    sampleCount: readings.length,
  };
}

const acknowledged = new Set();

export const mockApi = {
  summary: async () => {
    const readings = buildReadings(144);
    return buildSummary(readings[0], readings);
  },
  recent: async (limit = 144) => buildReadings(limit),
  history: async (days = 2) => buildHistoryHourly(days),
  alerts: async (includeAck = false) =>
    buildAlerts().filter((a) => includeAck || !acknowledged.has(a.id)),
  problems: async () => buildProblems(),
  systemChecks: async () => {
    const r = buildReadings(1);
    return buildSystemChecks(r[r.length - 1]);
  },
  acknowledge: async (id) => {
    acknowledged.add(id);
    return null;
  },
  predictions: async (hours = 24) => buildPredictions(hours),
};
