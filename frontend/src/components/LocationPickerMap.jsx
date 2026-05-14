import './leafletSetup';
import { MapContainer, TileLayer, Marker, useMapEvents } from 'react-leaflet';

const MOLDOVA_CENTER = [47.0105, 28.8638];
const MOLDOVA_ZOOM = 7;

function ClickHandler({ onPick }) {
  useMapEvents({
    click(e) {
      onPick(e.latlng.lat, e.latlng.lng);
    },
  });
  return null;
}

export default function LocationPickerMap({ value, onChange, height = 360 }) {
  const center = value ? [value.latitude, value.longitude] : MOLDOVA_CENTER;
  const zoom = value ? 11 : MOLDOVA_ZOOM;
  return (
    <div className="map-wrap" style={{ height }}>
      <MapContainer
        center={center}
        zoom={zoom}
        scrollWheelZoom
        style={{ width: '100%', height: '100%' }}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <ClickHandler onPick={(lat, lng) => onChange({ latitude: lat, longitude: lng })} />
        {value && <Marker position={[value.latitude, value.longitude]} />}
      </MapContainer>
      <div className="map-hint">Click anywhere on the map to place the marker</div>
    </div>
  );
}
