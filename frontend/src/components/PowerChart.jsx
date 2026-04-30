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

const fmtTime = (t) =>
  new Date(t).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

export default function PowerChart({ readings }) {
  const data = [...readings]
    .reverse()
    .map((r) => ({
      time: fmtTime(r.timestamp),
      pv: Math.round(r.data.pv_input_power ?? r.data.PvInputPower ?? 0),
      load: Math.round(
        r.data.ac_output_active_power ?? r.data.AcOutputActivePower ?? 0
      ),
    }));

  return (
    <div className="card">
      <div className="card__header">
        <h3>Power — last 12h</h3>
        <span className="card__sub">PV production vs. load</span>
      </div>
      <div className="card__body" style={{ height: 260 }}>
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={data} margin={{ top: 8, right: 16, left: -8, bottom: 0 }}>
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
            <XAxis dataKey="time" stroke="#64748b" tick={{ fontSize: 11 }} />
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
