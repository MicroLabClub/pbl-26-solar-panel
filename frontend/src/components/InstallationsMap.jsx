import './leafletSetup';
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet';
import { useEffect } from 'react';

// Republic of Moldova, centered, zoomed to fit the country.
const MOLDOVA_CENTER = [47.0105, 28.8638];
const MOLDOVA_ZOOM = 7;

function FitToInstallations({ installations }) {
  const map = useMap();
  useEffect(() => {
    if (!installations || installations.length === 0) return;
    if (installations.length === 1) {
      map.setView([installations[0].latitude, installations[0].longitude], 11);
      return;
    }
    const bounds = installations.map((i) => [i.latitude, i.longitude]);
    map.fitBounds(bounds, { padding: [40, 40], maxZoom: 12 });
  }, [installations, map]);
  return null;
}

export default function InstallationsMap({ installations, selectedId, onSelect, height = 480 }) {
  return (
    <div className="map-wrap" style={{ height }}>
      <MapContainer
        center={MOLDOVA_CENTER}
        zoom={MOLDOVA_ZOOM}
        scrollWheelZoom
        style={{ width: '100%', height: '100%' }}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <FitToInstallations installations={installations} />
        {installations.map((i) => (
          <Marker key={i.id} position={[i.latitude, i.longitude]}>
            <Popup>
              <div className="map-popup">
                <div className="map-popup__title">{i.name}</div>
                <div className="map-popup__row">
                  <span>Device:</span> <code>{i.mqttDeviceId}</code>
                </div>
                <div className="map-popup__row">
                  <span>Capacity:</span> {i.systemCapacityWatts} W
                </div>
                <div className="map-popup__row">
                  <span>Coords:</span> {i.latitude.toFixed(4)}, {i.longitude.toFixed(4)}
                </div>
                {onSelect && (
                  <button
                    className="btn btn--primary"
                    onClick={() => onSelect(i.id)}
                    disabled={selectedId === i.id}
                  >
                    {selectedId === i.id ? 'Currently monitoring' : 'Monitor this'}
                  </button>
                )}
              </div>
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  );
}
