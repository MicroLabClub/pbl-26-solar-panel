import { useEffect, useState } from 'react';
import Sidebar from './components/Sidebar';
import DashboardPage from './pages/DashboardPage';
import PredictionsPage from './pages/PredictionsPage';
import AlertsPage from './pages/AlertsPage';
import InstallationsPage from './pages/InstallationsPage';
import { api } from './services/api';
import { InstallationProvider } from './context/InstallationContext';

const PAGES = {
  dashboard: DashboardPage,
  installations: InstallationsPage,
  predictions: PredictionsPage,
  alerts: AlertsPage,
};

export default function App() {
  const [active, setActive] = useState(() => window.location.hash.replace('#', '') || 'dashboard');
  const [tick, setTick] = useState(0);

  useEffect(() => {
    window.location.hash = active;
  }, [active]);

  useEffect(() => {
    const t = setTimeout(() => setTick((n) => n + 1), 1500);
    return () => clearTimeout(t);
  }, []);

  const Page = PAGES[active] ?? DashboardPage;
  const usingMock = api.isMock();
  const statusLabel = usingMock ? 'Mock data' : 'Live';
  const statusVariant = usingMock ? 'mock' : 'ok';

  return (
    <InstallationProvider>
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
    </InstallationProvider>
  );
}
