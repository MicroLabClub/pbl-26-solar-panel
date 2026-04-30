import { useEffect, useState } from 'react';
import Sidebar from './components/Sidebar';
import DashboardPage from './pages/DashboardPage';
import PredictionsPage from './pages/PredictionsPage';
import AlertsPage from './pages/AlertsPage';
import { api } from './services/api';

const PAGES = {
  dashboard: DashboardPage,
  predictions: PredictionsPage,
  alerts: AlertsPage,
};

export default function App() {
  const [active, setActive] = useState(() => window.location.hash.replace('#', '') || 'dashboard');
  const [tick, setTick] = useState(0);

  useEffect(() => {
    window.location.hash = active;
  }, [active]);

  // Re-render once after first poll so the sidebar status pill reflects mock vs live.
  useEffect(() => {
    const t = setTimeout(() => setTick((n) => n + 1), 1500);
    return () => clearTimeout(t);
  }, []);

  const Page = PAGES[active] ?? DashboardPage;
  const usingMock = api.isMock();
  const statusLabel = usingMock ? 'Mock data' : 'Live';
  const statusVariant = usingMock ? 'mock' : 'ok';

  return (
    <div className="layout">
      <Sidebar
        active={active}
        onChange={setActive}
        statusLabel={statusLabel}
        statusVariant={statusVariant}
      />
      <main className="content">
        <Page key={active + tick} />
      </main>
    </div>
  );
}
