import { useEffect, useState } from 'react';
import Modal from './Modal';
import { api } from '../services/api';

export default function DeleteInstallationModal({ open, installation, onClose, onDeleted }) {
  const [typed, setTyped] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (open) {
      setTyped('');
      setError(null);
      setSubmitting(false);
    }
  }, [open]);

  if (!installation) return null;

  const nameMatches = typed.trim() === installation.name;

  async function handleDelete() {
    if (!nameMatches) return;
    setSubmitting(true);
    setError(null);
    try {
      await api.installationDelete(installation.id);
      onDeleted?.();
    } catch (e) {
      setError(e.message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Delete installation"
      footer={
        <>
          {error && <span className="modal__error">{error}</span>}
          <button className="btn" onClick={onClose} disabled={submitting}>Cancel</button>
          <button
            className="btn btn--danger"
            onClick={handleDelete}
            disabled={!nameMatches || submitting}
          >
            {submitting ? 'Deleting…' : 'Permanently delete'}
          </button>
        </>
      }
    >
      <div className="delete-warning">
        <p>
          This permanently removes the installation <strong>{installation.name}</strong> from the database.
        </p>
        <ul>
          <li>Telemetry already recorded for this installation stays in MongoDB but becomes orphaned (still queryable by its old <code>installation_id</code>, no longer shown anywhere in the app).</li>
          <li>Trained ML correction model for this site is left on disk; it will only get loaded again if you recreate an installation with the same ID.</li>
          <li>If the Pi keeps publishing for this device, new readings will be unassigned (no installation linked) until you create another installation with device id <code>{installation.mqttDeviceId}</code>.</li>
        </ul>
        <p className="delete-confirm-prompt">
          To confirm, type the installation name <strong>{installation.name}</strong> below:
        </p>
        <input
          className="delete-confirm-input"
          value={typed}
          onChange={(e) => setTyped(e.target.value)}
          placeholder={installation.name}
          autoFocus
        />
      </div>
    </Modal>
  );
}
