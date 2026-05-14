import { useState } from 'react';
import { usePolling } from '../hooks/usePolling';
import { api } from '../services/api';
import StatTile from '../components/StatTile';
import PowerChart from '../components/PowerChart';
import BatteryChart from '../components/BatteryChart';
import SystemTable from '../components/SystemTable';
import { useInstallation } from '../context/InstallationContext';

export default function DashboardPage() {
  const { selectedId, selected } = useInstallation();

  const summary = usePolling(() => api.summary(selectedId), 5000, [selectedId]);
  const latestRow = usePolling(() => api.recent(1, selectedId), 10000, [selectedId]);

  const [powerHours, setPowerHours] = useState(12);
  const [batteryHours, setBatteryHours] = useState(12);
  const powerSeries = usePolling(
    () => api.series(powerHours, selectedId),
    10000,
    [selectedId, powerHours]
  );
  const batterySeries = usePolling(
    () => api.series(batteryHours, selectedId),
    10000,
    [selectedId, batteryHours]
  );

  const s = summary.data ?? {};
  const latest = latestRow.data?.[0];

  return (
    <div className="page">
      <div className="page__header">
        <h2>Dashboard</h2>
        <span className="page__sub">
          Live operating data {selected ? `· ${selected.name}` : ''}
        </span>
      </div>

      <section className="tiles">
        <StatTile
          label="PV power now"
          value={s.currentPvPower != null ? Math.round(s.currentPvPower) : '—'}
          unit="W"
          accent="solar"
          sub={s.isCharging ? 'charging' : 'idle'}
        />
        <StatTile
          label="Load"
          value={s.currentLoad != null ? Math.round(s.currentLoad) : '—'}
          unit="W"
          accent="load"
          sub={s.isLoadOn ? 'load on' : 'load off'}
        />
        <StatTile
          label="Battery SOC"
          value={s.batterySoc != null ? Math.round(s.batterySoc) : '—'}
          unit="%"
          accent={s.batterySoc < 20 ? 'danger' : s.batterySoc < 40 ? 'warn' : 'battery'}
          sub={s.batteryVoltage ? `${s.batteryVoltage.toFixed(2)} V` : null}
        />
        <StatTile
          label="Heat sink"
          value={s.heatSinkC != null ? Math.round(s.heatSinkC) : '—'}
          unit="°C"
          accent={s.heatSinkC > 65 ? 'danger' : s.heatSinkC > 50 ? 'warn' : 'temp'}
        />
        <StatTile
          label="Grid"
          value={s.gridVoltage != null ? s.gridVoltage.toFixed(0) : '—'}
          unit="V"
          accent="grid"
        />
        <StatTile
          label="Energy today"
          value={s.todayEnergyKwh ?? '—'}
          unit="kWh"
          accent="energy"
        />
      </section>

      <section className="grid">
        <div className="grid__main">
          <PowerChart
            series={powerSeries.data ?? []}
            hours={powerHours}
            onRangeChange={setPowerHours}
          />
          <BatteryChart
            series={batterySeries.data ?? []}
            hours={batteryHours}
            onRangeChange={setBatteryHours}
          />
        </div>
        <div className="grid__side">
          <SystemTable latest={latest} />
        </div>
      </section>
    </div>
  );
}
