import { useMemo } from 'react';
import {
  ResponsiveContainer,
  ComposedChart,
  Area,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  Legend,
  ReferenceLine,
} from 'recharts';

function fmtHour(iso) {
  const d = new Date(iso);
  return d.getHours().toString().padStart(2, '0') + ':00';
}

export default function PredictedVsActualChart({ today }) {
  const data = useMemo(() => {
    if (!today?.hourly) return [];
    return today.hourly.map((p) => ({
      iso: p.hour,
      time: fmtHour(p.hour),
      predicted: Math.round(p.predictedWatts ?? 0),
      actual: p.actualWatts == null ? null : Math.round(p.actualWatts),
      cloud: Math.round(p.cloudCover ?? 0),
    }));
  }, [today]);

  const nowHour = useMemo(() => {
    const now = new Date();
    const hr = now.getHours().toString().padStart(2, '0') + ':00';
    return data.find((d) => d.time === hr)?.time;
  }, [data]);

  return (
    <ResponsiveContainer width="100%" height="100%">
      <ComposedChart data={data} margin={{ top: 10, right: 16, left: 0, bottom: 8 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
        <XAxis dataKey="time" stroke="#64748b" tick={{ fontSize: 11 }} />
        <YAxis
          yAxisId="power"
          stroke="#64748b"
          tick={{ fontSize: 11 }}
          label={{ value: 'W', position: 'insideTopLeft', fill: '#64748b', fontSize: 11 }}
        />
        <YAxis
          yAxisId="cloud"
          orientation="right"
          stroke="#475569"
          tick={{ fontSize: 11 }}
          domain={[0, 100]}
          label={{ value: '%', position: 'insideTopRight', fill: '#475569', fontSize: 11 }}
        />
        <Tooltip
          contentStyle={{
            background: '#0f172a',
            border: '1px solid #1e293b',
            borderRadius: 8,
          }}
        />
        <Legend wrapperStyle={{ fontSize: 12 }} />
        {nowHour && (
          <ReferenceLine
            yAxisId="power"
            x={nowHour}
            stroke="#64748b"
            strokeDasharray="4 4"
            label={{ value: 'now', fill: '#94a3b8', fontSize: 11, position: 'top' }}
          />
        )}
        <Area
          yAxisId="cloud"
          type="monotone"
          dataKey="cloud"
          name="Cloud cover %"
          fill="#1e293b"
          stroke="#334155"
          fillOpacity={0.5}
        />
        <Line
          yAxisId="power"
          type="monotone"
          dataKey="predicted"
          name="Predicted (model)"
          stroke="#facc15"
          strokeWidth={2}
          dot={false}
        />
        <Line
          yAxisId="power"
          type="monotone"
          dataKey="actual"
          name="Actual (live)"
          stroke="#22c55e"
          strokeWidth={2.5}
          dot={{ r: 3, fill: '#22c55e' }}
          connectNulls={false}
        />
      </ComposedChart>
    </ResponsiveContainer>
  );
}
