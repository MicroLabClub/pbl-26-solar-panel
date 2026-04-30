import { useMemo } from 'react';
import {
  ResponsiveContainer,
  ComposedChart,
  Bar,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  Legend,
  ReferenceLine,
} from 'recharts';
import { usePolling } from '../hooks/usePolling';
import { api } from '../services/api';
import StatTile from '../components/StatTile';

const FORECAST_HOURS = 72;
const HISTORY_DAYS = 2;

function fmtTick(iso) {
  const d = new Date(iso);
  const day = d.toLocaleDateString([], { weekday: 'short' });
  const hr = d.getHours().toString().padStart(2, '0');
  return `${day} ${hr}:00`;
}

export default function PredictionsPage() {
  const history = usePolling(() => api.history(HISTORY_DAYS), 60000);
  const prediction = usePolling(() => api.predictions(FORECAST_HOURS), 60000);

  const chartData = useMemo(() => {
    const past = (history.data ?? []).map((p) => ({
      iso: p.hour,
      time: fmtTick(p.hour),
      produced: Math.round(p.producedPowerWatts ?? p.ProducedPowerWatts ?? 0),
      forecast: null,
    }));
    const future = (prediction.data?.hourly ?? []).map((p) => ({
      iso: p.hour,
      time: fmtTick(p.hour),
      produced: null,
      forecast: Math.round(p.predictedPowerWatts ?? p.PredictedPowerWatts ?? 0),
    }));
    return [...past, ...future];
  }, [history.data, prediction.data]);

  const producedKwhTotal = useMemo(() => {
    if (!history.data) return null;
    const wh = history.data.reduce(
      (s, p) => s + (p.producedPowerWatts ?? p.ProducedPowerWatts ?? 0),
      0
    );
    return Math.round((wh / 1000) * 100) / 100;
  }, [history.data]);

  const forecastKwh = prediction.data?.totalPredictedEnergyKwh
    ?? prediction.data?.TotalPredictedEnergyKwh
    ?? null;

  const peakForecast = useMemo(() => {
    if (!prediction.data?.hourly) return null;
    return Math.max(
      ...prediction.data.hourly.map((h) => h.predictedPowerWatts ?? h.PredictedPowerWatts ?? 0)
    );
  }, [prediction.data]);

  const nowMarker = chartData.find((d) => d.forecast != null)?.time;

  return (
    <div className="page">
      <div className="page__header">
        <h2>Predictions</h2>
        <span className="page__sub">Last {HISTORY_DAYS} days produced vs. {FORECAST_HOURS}h forecast</span>
      </div>

      <section className="tiles">
        <StatTile
          label={`Produced last ${HISTORY_DAYS}d`}
          value={producedKwhTotal ?? '—'}
          unit="kWh"
          accent="energy"
        />
        <StatTile
          label={`Forecast next ${Math.round(FORECAST_HOURS / 24)}d`}
          value={forecastKwh ?? '—'}
          unit="kWh"
          accent="solar"
        />
        <StatTile
          label="Peak forecast power"
          value={peakForecast != null ? Math.round(peakForecast) : '—'}
          unit="W"
          accent="battery"
        />
        <StatTile
          label="Method"
          value={prediction.data?.method?.split('·')[0]?.trim() ?? '—'}
          sub={prediction.data?.method?.split('·')[1]?.trim() ?? ''}
          accent="grid"
        />
      </section>

      <div className="card">
        <div className="card__header">
          <h3>Energy production — past {HISTORY_DAYS} days &amp; forecast</h3>
          <span className="card__sub">Hourly average power · vertical line marks now</span>
        </div>
        <div className="card__body" style={{ height: 380 }}>
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={chartData} margin={{ top: 10, right: 16, left: 0, bottom: 8 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
              <XAxis
                dataKey="time"
                stroke="#64748b"
                tick={{ fontSize: 10 }}
                interval={Math.floor(chartData.length / 12)}
              />
              <YAxis
                stroke="#64748b"
                tick={{ fontSize: 11 }}
                label={{ value: 'W', position: 'insideTopLeft', fill: '#64748b', fontSize: 11 }}
              />
              <Tooltip
                contentStyle={{
                  background: '#0f172a',
                  border: '1px solid #1e293b',
                  borderRadius: 8,
                }}
              />
              <Legend wrapperStyle={{ fontSize: 12 }} />
              {nowMarker && (
                <ReferenceLine
                  x={nowMarker}
                  stroke="#64748b"
                  strokeDasharray="4 4"
                  label={{ value: 'now', fill: '#94a3b8', fontSize: 11, position: 'top' }}
                />
              )}
              <Bar
                dataKey="produced"
                name="Produced (actual)"
                fill="#22c55e"
                radius={[2, 2, 0, 0]}
              />
              <Line
                type="monotone"
                dataKey="forecast"
                name="Forecast"
                stroke="#facc15"
                strokeWidth={2}
                dot={false}
              />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="card">
        <div className="card__header">
          <h3>Daily breakdown</h3>
          <span className="card__sub">Past production aggregated by day</span>
        </div>
        <div className="card__body">
          <DailyBreakdown history={history.data ?? []} />
        </div>
      </div>
    </div>
  );
}

function DailyBreakdown({ history }) {
  const byDay = new Map();
  history.forEach((p) => {
    const day = new Date(p.hour).toDateString();
    const watts = p.producedPowerWatts ?? p.ProducedPowerWatts ?? 0;
    byDay.set(day, (byDay.get(day) ?? 0) + watts);
  });
  const rows = [...byDay.entries()].map(([day, wh]) => ({
    day,
    kwh: Math.round((wh / 1000) * 100) / 100,
  }));

  if (rows.length === 0) return <div style={{ color: 'var(--muted)' }}>No history yet.</div>;

  const max = Math.max(...rows.map((r) => r.kwh));

  return (
    <div className="daily">
      {rows.map((r) => (
        <div key={r.day} className="daily__row">
          <div className="daily__day">{r.day}</div>
          <div className="daily__bar">
            <div
              className="daily__fill"
              style={{ width: `${Math.max(2, (r.kwh / max) * 100)}%` }}
            />
          </div>
          <div className="daily__val">{r.kwh} kWh</div>
        </div>
      ))}
    </div>
  );
}
