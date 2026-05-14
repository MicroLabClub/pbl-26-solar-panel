import { useState } from 'react';
import { useInstallation } from '../context/InstallationContext';
import InstallationsMap from '../components/InstallationsMap';
import InstallationFormModal from '../components/InstallationFormModal';
import DeleteInstallationModal from '../components/DeleteInstallationModal';

export default function InstallationsPage() {
  const { installations, selectedId, select, loading, error, refresh } = useInstallation();
  const [view, setView] = useState('list');
  const [formMode, setFormMode] = useState(null); // null | { mode: 'create' } | { mode: 'edit', installation }
  const [deleteTarget, setDeleteTarget] = useState(null);

  async function handleSaved() {
    setFormMode(null);
    await refresh();
  }

  async function handleDeleted() {
    setDeleteTarget(null);
    await refresh();
  }

  return (
    <div className="page">
      <div className="page__header">
        <h2>Installations</h2>
        <span className="page__sub">
          {installations.length} configured · the selected one drives all other pages
        </span>
      </div>

      <div className="toolbar">
        <div className="view-toggle">
          <button
            className={`tab ${view === 'list' ? 'tab--active' : ''}`}
            onClick={() => setView('list')}
          >
            List
          </button>
          <button
            className={`tab ${view === 'map' ? 'tab--active' : ''}`}
            onClick={() => setView('map')}
          >
            Map
          </button>
        </div>
        <button className="btn btn--primary" onClick={() => setFormMode({ mode: 'create' })}>
          + Add installation
        </button>
      </div>

      {loading && <div className="card"><div className="card__body">Loading installations…</div></div>}
      {error && (
        <div className="card">
          <div className="card__body" style={{ color: '#f87171' }}>
            {error.message ?? String(error)}
          </div>
        </div>
      )}

      {view === 'list' && !loading && (
        <div className="install-grid">
          {installations.map((i) => (
            <div
              key={i.id}
              className={`install-card ${selectedId === i.id ? 'install-card--selected' : ''}`}
            >
              <div className="install-card__head">
                <h3>{i.name}</h3>
                {selectedId === i.id && <span className="pill pill--info">Monitoring</span>}
              </div>
              <div className="install-card__field">
                <span>Device:</span> <code>{i.mqttDeviceId}</code>
              </div>
              <div className="install-card__field">
                <span>Capacity:</span> {i.systemCapacityWatts} W
              </div>
              <div className="install-card__field">
                <span>Location:</span> {i.latitude.toFixed(4)}, {i.longitude.toFixed(4)}
              </div>
              <div className="install-card__field">
                <span>Timezone:</span> {i.timezone}
              </div>
              {i.notes && <div className="install-card__notes">{i.notes}</div>}
              <div className="install-card__actions">
                <button
                  className="btn btn--primary"
                  onClick={() => select(i.id)}
                  disabled={selectedId === i.id}
                >
                  {selectedId === i.id ? 'Currently monitoring' : 'Monitor this'}
                </button>
                <button
                  className="btn"
                  onClick={() => setFormMode({ mode: 'edit', installation: i })}
                >
                  Edit
                </button>
              </div>
            </div>
          ))}
          {installations.length === 0 && (
            <div className="card">
              <div className="card__body" style={{ color: 'var(--muted)' }}>
                No installations yet. Click <strong>+ Add installation</strong> to create one.
              </div>
            </div>
          )}
        </div>
      )}

      {view === 'map' && (
        <div className="card">
          <div className="card__header">
            <h3>Map</h3>
            <span className="card__sub">
              Focused on Moldova · click a pin to view and select
            </span>
          </div>
          <div className="card__body" style={{ padding: 0 }}>
            <InstallationsMap
              installations={installations}
              selectedId={selectedId}
              onSelect={select}
              height={520}
            />
          </div>
        </div>
      )}

      <InstallationFormModal
        open={formMode !== null}
        mode={formMode?.mode ?? 'create'}
        installation={formMode?.installation ?? null}
        onClose={() => setFormMode(null)}
        onSaved={handleSaved}
        onRequestDelete={(target) => {
          setFormMode(null);
          setDeleteTarget(target);
        }}
      />
      <DeleteInstallationModal
        open={deleteTarget !== null}
        installation={deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onDeleted={handleDeleted}
      />
    </div>
  );
}
