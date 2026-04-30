export default function StatTile({ label, value, unit, accent, sub }) {
  return (
    <div className={`tile ${accent ? `tile--${accent}` : ''}`}>
      <div className="tile__label">{label}</div>
      <div className="tile__value">
        {value}
        {unit && <span className="tile__unit">{unit}</span>}
      </div>
      {sub && <div className="tile__sub">{sub}</div>}
    </div>
  );
}
