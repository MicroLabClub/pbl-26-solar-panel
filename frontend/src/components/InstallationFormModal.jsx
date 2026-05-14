import { useEffect, useState } from 'react';
import Modal from './Modal';
import LocationPickerMap from './LocationPickerMap';
import { api } from '../services/api';

const COORD_MODES = [
  { key: 'address', label: 'Address' },
  { key: 'manual', label: 'Coordinates' },
  { key: 'map', label: 'Pick on map' },
];

async function geocode(query) {
  // Routes through our backend to Nominatim (street-level results).
  const r = await fetch(`/api/geocode?q=${encodeURIComponent(query)}`);
  if (!r.ok) throw new Error(`geocode failed: ${r.status}`);
  return r.json();
}

const DEFAULTS = {
  name: '',
  mqttDeviceId: '',
  systemCapacityWatts: 3000,
  panelTiltDeg: 30,
  panelAzimuthDeg: 180,
  timezone: 'Europe/Chisinau',
  notes: '',
};

function formFromInstallation(i) {
  return {
    name: i.name ?? '',
    mqttDeviceId: i.mqttDeviceId ?? '',
    systemCapacityWatts: i.systemCapacityWatts ?? 3000,
    panelTiltDeg: i.panelTiltDeg ?? 30,
    panelAzimuthDeg: i.panelAzimuthDeg ?? 180,
    timezone: i.timezone ?? 'Europe/Chisinau',
    notes: i.notes ?? '',
  };
}

export default function InstallationFormModal({
  open,
  mode = 'create',
  installation = null,
  onClose,
  onSaved,
  onRequestDelete,
}) {
  const [form, setForm] = useState(DEFAULTS);
  const [coordMode, setCoordMode] = useState('address');
  const [coords, setCoords] = useState(null);
  const [addressQuery, setAddressQuery] = useState('');
  const [geocodeResults, setGeocodeResults] = useState([]);
  const [geocoding, setGeocoding] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!open) return;
    if (mode === 'edit' && installation) {
      setForm(formFromInstallation(installation));
      setCoords({ latitude: installation.latitude, longitude: installation.longitude });
      setCoordMode('map');
    } else {
      setForm(DEFAULTS);
      setCoords(null);
      setCoordMode('address');
    }
    setAddressQuery('');
    setGeocodeResults([]);
    setError(null);
  }, [open, mode, installation]);

  function setField(k, v) {
    setForm((f) => ({ ...f, [k]: v }));
  }

  async function handleGeocode() {
    if (!addressQuery.trim()) return;
    setGeocoding(true);
    setError(null);
    try {
      const results = await geocode(addressQuery);
      setGeocodeResults(results);
      if (results.length === 0) setError('No results — try a different query');
    } catch (e) {
      setError(e.message);
    } finally {
      setGeocoding(false);
    }
  }

  function pickGeocodeResult(r) {
    setCoords({ latitude: r.latitude, longitude: r.longitude });
    setForm((f) => ({ ...f, timezone: r.timezone || f.timezone }));
  }

  function setManualCoord(field, val) {
    const n = parseFloat(val);
    setCoords((c) => ({
      latitude: field === 'lat' ? n : c?.latitude ?? 0,
      longitude: field === 'lon' ? n : c?.longitude ?? 0,
    }));
  }

  async function handleSubmit(e) {
    e?.preventDefault?.();
    setError(null);
    if (!form.name.trim()) return setError('Name is required');
    if (!form.mqttDeviceId.trim()) return setError('MQTT device ID is required');
    if (!coords || !Number.isFinite(coords.latitude) || !Number.isFinite(coords.longitude))
      return setError('Coordinates are required — set them via address, manual, or map');

    setSubmitting(true);
    try {
      const payload = {
        ...form,
        systemCapacityWatts: Number(form.systemCapacityWatts) || 0,
        panelTiltDeg: Number(form.panelTiltDeg) || 30,
        panelAzimuthDeg: Number(form.panelAzimuthDeg) || 180,
        latitude: coords.latitude,
        longitude: coords.longitude,
      };
      const saved =
        mode === 'edit'
          ? await api.installationUpdate(installation.id, { ...installation, ...payload })
          : await api.installationCreate(payload);
      onSaved?.(saved);
    } catch (e) {
      setError(e.message);
    } finally {
      setSubmitting(false);
    }
  }

  const coordSummary =
    coords && Number.isFinite(coords.latitude) && Number.isFinite(coords.longitude)
      ? `${coords.latitude.toFixed(4)}, ${coords.longitude.toFixed(4)}`
      : '(not set)';

  const title = mode === 'edit' ? `Edit · ${installation?.name ?? ''}` : 'Add installation';
  const submitLabel =
    mode === 'edit'
      ? submitting ? 'Saving…' : 'Save changes'
      : submitting ? 'Creating…' : 'Create installation';

  return (
    <Modal
      open={open}
      onClose={onClose}
      wide
      title={title}
      footer={
        <>
          {mode === 'edit' && onRequestDelete && (
            <button
              type="button"
              className="btn btn--danger"
              onClick={() => onRequestDelete(installation)}
              disabled={submitting}
            >
              Delete
            </button>
          )}
          {error && <span className="modal__error">{error}</span>}
          <span className="modal__spacer" />
          <button className="btn" onClick={onClose} disabled={submitting}>Cancel</button>
          <button className="btn btn--primary" onClick={handleSubmit} disabled={submitting}>
            {submitLabel}
          </button>
        </>
      }
    >
      <form className="form" onSubmit={handleSubmit}>
        <div className="form__row">
          <label>Name</label>
          <input value={form.name} onChange={(e) => setField('name', e.target.value)} placeholder="e.g. Roof — main house" />
        </div>
        <div className="form__row">
          <label>MQTT device ID</label>
          <input
            value={form.mqttDeviceId}
            onChange={(e) => setField('mqttDeviceId', e.target.value)}
            placeholder="e.g. pi-roof-01"
            disabled={mode === 'edit'}
            title={mode === 'edit' ? 'Device ID is immutable once set' : ''}
          />
        </div>
        <div className="form__grid">
          <div className="form__row">
            <label>System capacity (W)</label>
            <input type="number" value={form.systemCapacityWatts} onChange={(e) => setField('systemCapacityWatts', e.target.value)} />
          </div>
          <div className="form__row">
            <label>Timezone</label>
            <input value={form.timezone} onChange={(e) => setField('timezone', e.target.value)} />
          </div>
          <div className="form__row">
            <label>Panel tilt (°)</label>
            <input type="number" value={form.panelTiltDeg} onChange={(e) => setField('panelTiltDeg', e.target.value)} />
          </div>
          <div className="form__row">
            <label>Panel azimuth (°, 180 = south)</label>
            <input type="number" value={form.panelAzimuthDeg} onChange={(e) => setField('panelAzimuthDeg', e.target.value)} />
          </div>
        </div>
        <div className="form__row">
          <label>Notes</label>
          <textarea value={form.notes} onChange={(e) => setField('notes', e.target.value)} rows={2} />
        </div>

        <div className="form__section">
          <div className="form__section-head">
            <span>Location</span>
            <span className="form__section-value">{coordSummary}</span>
          </div>
          <div className="tabs">
            {COORD_MODES.map((m) => (
              <button
                type="button"
                key={m.key}
                className={`tab ${coordMode === m.key ? 'tab--active' : ''}`}
                onClick={() => setCoordMode(m.key)}
              >
                {m.label}
              </button>
            ))}
          </div>

          {coordMode === 'address' && (
            <div className="coord-mode">
              <div className="form__row form__row--inline">
                <input
                  value={addressQuery}
                  onChange={(e) => setAddressQuery(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), handleGeocode())}
                  placeholder="e.g. Chișinău, Moldova"
                />
                <button type="button" className="btn" onClick={handleGeocode} disabled={geocoding}>
                  {geocoding ? 'Searching…' : 'Resolve'}
                </button>
              </div>
              {geocodeResults.length > 0 && (
                <div className="geocode-results">
                  {geocodeResults.map((r, idx) => (
                    <button
                      type="button"
                      key={`${idx}-${r.latitude}-${r.longitude}`}
                      className={`geocode-result ${
                        coords?.latitude === r.latitude && coords?.longitude === r.longitude
                          ? 'geocode-result--selected'
                          : ''
                      }`}
                      onClick={() => pickGeocodeResult(r)}
                    >
                      <div className="geocode-result__name">{r.name}</div>
                      <div className="geocode-result__detail">
                        {[r.locality, r.admin1, r.country].filter(Boolean).join(' · ')} ·{' '}
                        {r.latitude.toFixed(6)}, {r.longitude.toFixed(6)}
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}

          {coordMode === 'manual' && (
            <div className="coord-mode form__grid">
              <div className="form__row">
                <label>Latitude</label>
                <input type="number" step="0.0001" value={coords?.latitude ?? ''} onChange={(e) => setManualCoord('lat', e.target.value)} />
              </div>
              <div className="form__row">
                <label>Longitude</label>
                <input type="number" step="0.0001" value={coords?.longitude ?? ''} onChange={(e) => setManualCoord('lon', e.target.value)} />
              </div>
            </div>
          )}

          {coordMode === 'map' && (
            <div className="coord-mode">
              <LocationPickerMap value={coords} onChange={setCoords} height={320} />
            </div>
          )}
        </div>
      </form>
    </Modal>
  );
}
