export const RANGES = [
  { hours: 3,   label: '3h',  long: 'Last 3 hours' },
  { hours: 12,  label: '12h', long: 'Last 12 hours' },
  { hours: 72,  label: '3d',  long: 'Last 3 days' },
  { hours: 168, label: '7d',  long: 'Last 7 days' },
];

export function rangeLabel(hours) {
  return RANGES.find((r) => r.hours === hours)?.long ?? `Last ${hours}h`;
}

export default function TimeRangeSelector({ value, onChange }) {
  return (
    <div className="range-selector">
      {RANGES.map((r) => (
        <button
          key={r.hours}
          className={`range-selector__btn ${value === r.hours ? 'range-selector__btn--active' : ''}`}
          onClick={() => onChange(r.hours)}
          title={r.long}
        >
          {r.label}
        </button>
      ))}
    </div>
  );
}
