import { useInstallation } from '../context/InstallationContext';

const NAV = [
  { key: 'dashboard', label: 'Dashboard', icon: '⌂' },
  { key: 'installations', label: 'Installations', icon: '⛯' },
  { key: 'predictions', label: 'Predictions', icon: '◴' },
  { key: 'alerts', label: 'Alerts', icon: '⚠' },
];

export default function Sidebar({ active, onChange, statusLabel, statusVariant }) {
  const { selected, installations } = useInstallation();

  return (
    <aside className="sidebar">
      <div className="sidebar__brand">
        <div className="sidebar__logo">☀</div>
        <div className="sidebar__brand-text">
          <div className="sidebar__title">Solar Monitor</div>
          <div className="sidebar__sub" title={selected?.mqttDeviceId ?? ''}>
            {selected ? selected.name : installations.length === 0 ? 'No installation' : 'Select installation'}
          </div>
        </div>
      </div>

      <nav className="sidebar__nav">
        {NAV.map((item) => (
          <button
            key={item.key}
            className={`sidebar__item ${active === item.key ? 'sidebar__item--active' : ''}`}
            onClick={() => onChange(item.key)}
          >
            <span className="sidebar__icon">{item.icon}</span>
            <span>{item.label}</span>
          </button>
        ))}
      </nav>

      <div className={`sidebar__status status status--${statusVariant}`}>
        <span className="status__dot" />
        {statusLabel}
      </div>
    </aside>
  );
}
