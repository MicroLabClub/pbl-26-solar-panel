import { useMemo, useState } from 'react';
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
import PredictedVsActualChart from '../components/PredictedVsActualChart';
import TimeRangeSelector, { rangeLabel } from '../components/TimeRangeSelector';
import { useInstallation } from '../context/InstallationContext';

const FORECAST_HOURS = 72;

function fmtTick(iso) {
  const d = new Date(iso);
  const day = d.toLocaleDateString([], { weekday: 'short' });
  const hr = d.getHours().toString().padStart(2, '0');
  return `${day} ${hr}:00`;
}

export default function PredictionsPage() {
  const { selectedId, selected } = useInstallation();

  const [historyHours, setHistoryHours] = useState(72);
  const historySeries = usePolling(
    () => api.series(historyHours, selectedId),
    60000,
    [selectedId, historyHours]
  );
  const prediction = usePolling(
    () => (selectedId ? api.installationPredictions(selectedId, FORECAST_HOURS) : Promise.resolve(null)),
    60000,
    [selectedId]
  );
  const today = usePolling(
    () => (selectedId ? api.installationPredictionsToday(selectedId) : Promise.resolve(null)),
    5000,
    [selectedId]
  );

  const [training, setTraining] = useState(false);
  const [trainResult, setTrainResult] = useState(null);

  async function handleTrain() {
    if (!selectedId) return;
    setTraining(true);
    setTrainResult(null);
    try {
      const r = await api.installationTrain(selectedId);
      setTrainResult(r);
    } catch (e) {
      setTrainResult({ trained: false, message: e.message });
    } finally {
      setTraining(false);
    }
  }

  const chartData = useMemo(() => {
    const past = (historySeries.data ?? []).map((p) => ({
      iso: p.timestamp,
      time: fmtTick(p.timestamp),
      produced: Math.round(p.pvPowerWatts ?? 0),
      forecast: null,
    }));
    const future = (prediction.data?.hourly ?? []).map((p) => ({
      iso: p.hour,
      time: fmtTick(p.hour),
      produced: null,
      forecast: Math.round(p.predictedPowerWatts ?? 0),
    }));
    return [...past, ...future];
  }, [historySeries.data, prediction.data]);

  const forecastKwh = prediction.data?.totalPredictedEnergyKwh ?? null;
  const peakForecast = useMemo(() => {
    if (!prediction.data?.hourly) return null;
    return Math.max(...prediction.data.hourly.map((h) => h.predictedPowerWatts ?? 0));
  }, [prediction.data]);
  const method = prediction.data?.method ?? today.data?.method ?? '—';
  const mae = today.data?.meanAbsoluteErrorWatts ?? null;
  const nowMarker = chartData.find((d) => d.forecast != null)?.time;

  return (
    <div className="page">
      <div className="page__header">
        <h2>Predictions</h2>
        <span className="page__sub">
          Hybrid forecast · physics + ML{selected ? ` · ${selected.name}` : ''}
        </span>
      </div>

      <div className="toolbar">
        <button className="btn" onClick={handleTrain} disabled={training || !selectedId}>
          {training ? 'Training…' : 'Train model now'}
        </button>
        {trainResult && (
          <span className={`train-result ${trainResult.trained ? 'ok' : 'warn'}`}>
            {trainResult.message}
          </span>
        )}
      </div>

      <section className="tiles">
        <StatTile label="Forecast next 3d" value={forecastKwh ?? '—'} unit="kWh" accent="solar" />
        <StatTile
          label="Peak forecast power"
          value={peakForecast != null ? Math.round(peakForecast) : '—'}
          unit="W"
          accent="battery"
        />
        <StatTile
          label="Today MAE"
          value={mae ?? '—'}
          unit="W"
          accent={mae == null || mae > 500 ? 'grid' : 'energy'}
          sub="predicted vs actual"
        />
        <StatTile
          label="Method"
          value={method.includes('ML') ? 'physics + ML' : 'physics only'}
          sub={method.includes('ML') ? 'correction active' : 'awaiting training data'}
          accent={method.includes('ML') ? 'energy' : 'grid'}
        />
      </section>

      <div className="card">
        <div className="card__header">
          <h3>Today — predicted vs actual</h3>
          <span className="card__sub">Live · refreshes every 5s</span>
        </div>
        <div className="card__body" style={{ height: 380 }}>
          {today.data ? (
            <PredictedVsActualChart today={today.data} />
          ) : (
            <div style={{ color: 'var(--muted)' }}>
              {selectedId ? 'Loading prediction…' : 'Select an installation first.'}
            </div>
          )}
        </div>
      </div>

      <div className="card">
        <div className="card__header card__header--row">
          <div>
            <h3>Past &amp; forecast · {rangeLabel(historyHours)} + {Math.round(FORECAST_HOURS / 24)}-day forecast</h3>
            <span className="card__sub">Average power · vertical line marks now</span>
          </div>
          <TimeRangeSelector value={historyHours} onChange={setHistoryHours} />
        </div>
        <div className="card__body" style={{ height: 320 }}>
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
              <Bar dataKey="produced" name="Produced (actual)" fill="#22c55e" radius={[2, 2, 0, 0]} />
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
    </div>
  );
}
