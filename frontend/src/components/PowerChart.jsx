import { useMemo } from 'react';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  Legend,
} from 'recharts';
import TimeRangeSelector, { rangeLabel } from './TimeRangeSelector';

function fmtTick(iso, hours) {
  const d = new Date(iso);
  if (hours <= 12) {
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  return `${d.toLocaleDateString([], { month: 'short', day: 'numeric' })} ${d.getHours().toString().padStart(2, '0')}:00`;
}

export default function PowerChart({ series, hours, onRangeChange }) {
  const data = useMemo(
    () =>
      (series ?? []).map((p) => ({
        time: fmtTick(p.timestamp, hours),
        pv: Math.round(p.pvPowerWatts ?? 0),
        load: Math.round(p.loadWatts ?? 0),
      })),
    [series, hours]
  );
  const xInterval = data.length > 24 ? Math.floor(data.length / 12) : 0;

  return (
    <div className="card">
      <div className="card__header card__header--row">
        <div>
          <h3>Power · {rangeLabel(hours)}</h3>
          <span className="card__sub">PV production vs. load</span>
        </div>
        <TimeRangeSelector value={hours} onChange={onRangeChange} />
      </div>
      <div className="card__body" style={{ height: 260 }}>
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={data} margin={{ top: 10, right: 16, left: 12, bottom: 0 }}>
            <defs>
              <linearGradient id="pvGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#facc15" stopOpacity={0.7} />
                <stop offset="100%" stopColor="#facc15" stopOpacity={0} />
              </linearGradient>
              <linearGradient id="loadGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="#38bdf8" stopOpacity={0.5} />
                <stop offset="100%" stopColor="#38bdf8" stopOpacity={0} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
            <XAxis dataKey="time" stroke="#64748b" tick={{ fontSize: 11 }} interval={xInterval} />
            <YAxis
              stroke="#64748b"
              tick={{ fontSize: 11 }}
              width={56}
              label={{
                value: 'W',
                angle: -90,
                position: 'insideLeft',
                fill: '#64748b',
                fontSize: 11,
                dy: 30,
              }}
            />
            <Tooltip
              contentStyle={{ background: '#0f172a', border: '1px solid #1e293b', borderRadius: 8 }}
            />
            <Legend wrapperStyle={{ fontSize: 12 }} />
            <Area
              type="monotone"
              dataKey="pv"
              name="PV power"
              stroke="#facc15"
              fill="url(#pvGrad)"
              strokeWidth={2}
            />
            <Area
              type="monotone"
              dataKey="load"
              name="Load"
              stroke="#38bdf8"
              fill="url(#loadGrad)"
              strokeWidth={2}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
