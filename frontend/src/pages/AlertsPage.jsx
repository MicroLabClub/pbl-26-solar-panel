import { usePolling } from '../hooks/usePolling';
import { api } from '../services/api';

const STATUS_LABELS = {
  ok: 'Running normally',
  warn: 'Needs attention',
  critical: 'Action required',
};

const SEV_VARIANT = {
  Critical: 'critical',
  Warning: 'warning',
  Info: 'info',
};

function fmtDuration(start, end) {
  const startMs = new Date(start).getTime();
  const endMs = end ? new Date(end).getTime() : Date.now();
  const minutes = Math.max(1, Math.round((endMs - startMs) / 60_000));
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const rem = minutes % 60;
  if (hours < 24) return rem ? `${hours}h ${rem}m` : `${hours}h`;
  const days = Math.floor(hours / 24);
  const remH = hours % 24;
  return remH ? `${days}d ${remH}h` : `${days}d`;
}

function fmtTime(iso) {
  return new Date(iso).toLocaleString([], {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export default function AlertsPage() {
  const checks = usePolling(api.systemChecks, 6000);
  const problems = usePolling(api.problems, 15000);

  const checkList = checks.data ?? [];
  const okCount = checkList.filter((c) => c.status === 'ok').length;
  const warnCount = checkList.filter((c) => c.status === 'warn').length;
  const critCount = checkList.filter((c) => c.status === 'critical').length;

  const problemList = problems.data ?? [];
  const active = problemList.filter((p) => !p.endedAt && !p.EndedAt);
  const resolved = problemList.filter((p) => p.endedAt || p.EndedAt);

  return (
    <div className="page">
      <div className="page__header">
        <h2>Alerts</h2>
        <span className="page__sub">System health and problem history</span>
      </div>

      <section className="alert-summary">
        <div className="alert-summary__card alert-summary__card--ok">
          <div className="alert-summary__count">{okCount}</div>
          <div className="alert-summary__label">Running fine</div>
        </div>
        <div className="alert-summary__card alert-summary__card--warn">
          <div className="alert-summary__count">{warnCount}</div>
          <div className="alert-summary__label">Warnings</div>
        </div>
        <div className="alert-summary__card alert-summary__card--critical">
          <div className="alert-summary__count">{critCount}</div>
          <div className="alert-summary__label">Critical</div>
        </div>
        <div className="alert-summary__card alert-summary__card--problems">
          <div className="alert-summary__count">{active.length}</div>
          <div className="alert-summary__label">Active problems</div>
        </div>
      </section>

      <div className="card">
        <div className="card__header">
          <h3>Subsystem status</h3>
          <span className="card__sub">What is running well, what is not</span>
        </div>
        <div className="card__body">
          <div className="checks">
            {checkList.map((c) => (
              <div key={c.key} className={`check check--${c.status}`}>
                <div className="check__top">
                  <span className={`check__dot check__dot--${c.status}`} />
                  <span className="check__label">{c.label}</span>
                  <span className="check__status">{STATUS_LABELS[c.status]}</span>
                </div>
                <div className="check__detail">{c.detail}</div>
              </div>
            ))}
            {checkList.length === 0 && (
              <div style={{ color: 'var(--muted)' }}>Loading subsystem checks…</div>
            )}
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card__header">
          <h3>Problems &amp; intervals</h3>
          <span className="card__sub">
            {active.length} active · {resolved.length} resolved in history
          </span>
        </div>
        <div className="card__body">
          <table className="problems">
            <thead>
              <tr>
                <th>Severity</th>
                <th>Problem</th>
                <th>Started</th>
                <th>Ended</th>
                <th>Duration</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {problemList.map((p) => {
                const startedAt = p.startedAt ?? p.StartedAt;
                const endedAt = p.endedAt ?? p.EndedAt;
                const sev = p.severity ?? p.Severity;
                return (
                  <tr key={p.id ?? p.Id}>
                    <td>
                      <span className={`pill pill--${SEV_VARIANT[sev] ?? 'info'}`}>{sev}</span>
                    </td>
                    <td>
                      <div className="problems__title">{p.title ?? p.Title}</div>
                      <div className="problems__msg">{p.message ?? p.Message}</div>
                    </td>
                    <td className="problems__time">{fmtTime(startedAt)}</td>
                    <td className="problems__time">
                      {endedAt ? fmtTime(endedAt) : <em>ongoing</em>}
                    </td>
                    <td className="problems__time">{fmtDuration(startedAt, endedAt)}</td>
                    <td>
                      {endedAt ? (
                        <span className="pill pill--info">Resolved</span>
                      ) : (
                        <span className="pill pill--critical">Active</span>
                      )}
                    </td>
                  </tr>
                );
              })}
              {problemList.length === 0 && (
                <tr>
                  <td colSpan={6} style={{ color: 'var(--muted)', textAlign: 'center', padding: 16 }}>
                    No problems recorded.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
