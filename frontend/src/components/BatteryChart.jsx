import { useMemo } from 'react';
import {
  ResponsiveContainer,
  ComposedChart,
  Bar,
  Line,
  Cell,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  Legend,
  ReferenceLine,
} from 'recharts';
import TimeRangeSelector, { rangeLabel } from './TimeRangeSelector';

function fmtTick(iso, hours) {
  const d = new Date(iso);
  if (hours <= 12) {
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  return `${d.toLocaleDateString([], { month: 'short', day: 'numeric' })} ${d.getHours().toString().padStart(2, '0')}:00`;
}

const LEGEND_PAYLOAD = [
  { value: 'Battery SOC %', type: 'line', color: '#a78bfa' },
  { value: 'Charging (+W)', type: 'square', color: '#22c55e' },
  { value: 'Discharging (−W)', type: 'square', color: '#ef4444' },
];

export default function BatteryChart({ series, hours, onRangeChange }) {
  const data = useMemo(
    () =>
      (series ?? []).map((p) => {
        const rawSoc = p.batterySoc ?? 0;
        return {
          time: fmtTick(p.timestamp, hours),
          // Treat SOC ≤ 5 as a transient bad reading: render a gap, not a 0% spike.
          soc: rawSoc > 5 ? Math.round(rawSoc) : null,
          flow: Math.round(p.batteryFlowWatts ?? 0),
        };
      }),
    [series, hours]
  );
  const xInterval = data.length > 24 ? Math.floor(data.length / 12) : 0;

  const chargedWh = useMemo(() => {
    if (data.length < 2) return 0;
    let wh = 0;
    for (let i = 1; i < data.length; i++) {
      const a = Math.max(0, data[i - 1].flow);
      const b = Math.max(0, data[i].flow);
      const dtHours = hours <= 12 ? 1 / 60 : 1;
      wh += ((a + b) / 2) * dtHours;
    }
    return wh;
  }, [data, hours]);

  return (
    <div className="card">
      <div className="card__header card__header--row">
        <div>
          <h3>Battery · {rangeLabel(hours)}</h3>
          <span className="card__sub">
            State of charge &amp; charge/discharge flow ·{' '}
            charged ~{Math.round(chargedWh).toLocaleString()} Wh in window
          </span>
        </div>
        <TimeRangeSelector value={hours} onChange={onRangeChange} />
      </div>
      <div className="card__body" style={{ height: 280 }}>
        <ResponsiveContainer width="100%" height="100%">
          <ComposedChart data={data} margin={{ top: 10, right: 28, left: 12, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
            <XAxis dataKey="time" stroke="#64748b" tick={{ fontSize: 11 }} interval={xInterval} />
            <YAxis
              yAxisId="soc"
              stroke="#64748b"
              tick={{ fontSize: 11 }}
              domain={[0, 100]}
              width={56}
              label={{
                value: 'SOC %',
                angle: -90,
                position: 'insideLeft',
                fill: '#64748b',
                fontSize: 11,
                dy: 30,
              }}
            />
            <YAxis
              yAxisId="flow"
              orientation="right"
              stroke="#475569"
              tick={{ fontSize: 11 }}
              width={56}
              label={{
                value: 'W',
                angle: -90,
                position: 'insideRight',
                fill: '#475569',
                fontSize: 11,
                dy: 10,
              }}
            />
            <Tooltip
              contentStyle={{ background: '#0f172a', border: '1px solid #1e293b', borderRadius: 8 }}
            />
            <Legend wrapperStyle={{ fontSize: 12 }} payload={LEGEND_PAYLOAD} />
            <ReferenceLine yAxisId="flow" y={0} stroke="#475569" />
            <Bar
              yAxisId="flow"
              dataKey="flow"
              name="flow"
              fill="#22c55e"
              isAnimationActive={false}
            >
              {data.map((d, i) => (
                <Cell key={i} fill={d.flow >= 0 ? '#22c55e' : '#ef4444'} />
              ))}
            </Bar>
            <Line
              yAxisId="soc"
              type="monotone"
              dataKey="soc"
              name="soc"
              stroke="#a78bfa"
              strokeWidth={2.5}
              dot={false}
              connectNulls={false}
              isAnimationActive={false}
            />
          </ComposedChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
