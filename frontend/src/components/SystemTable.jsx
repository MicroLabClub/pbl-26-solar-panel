function row(label, value, unit) {
  return (
    <tr>
      <td className="sys__label">{label}</td>
      <td className="sys__value">
        {value}
        {unit && <span className="sys__unit"> {unit}</span>}
      </td>
    </tr>
  );
}

export default function SystemTable({ latest }) {
  if (!latest) return null;
  const d = latest.data;

  return (
    <div className="card">
      <div className="card__header">
        <h3>Inverter status</h3>
        <span className="card__sub">
          updated {new Date(latest.timestamp).toLocaleTimeString()}
        </span>
      </div>
      <div className="card__body">
        <table className="sys">
          <tbody>
            {row('AC input voltage', d.ac_input_voltage?.toFixed(1), 'V')}
            {row('AC input frequency', d.ac_input_frequency?.toFixed(1), 'Hz')}
            {row('AC output voltage', d.ac_output_voltage?.toFixed(1), 'V')}
            {row('AC output frequency', d.ac_output_frequency?.toFixed(1), 'Hz')}
            {row('AC output power', Math.round(d.ac_output_active_power), 'W')}
            {row('Output load', d.ac_output_load?.toFixed(0), '%')}
            {row('Bus voltage', d.bus_voltage?.toFixed(0), 'V')}
            {row('Battery voltage', d.battery_voltage?.toFixed(2), 'V')}
            {row('Battery charging current', d.battery_charging_current?.toFixed(1), 'A')}
            {row('Battery discharge current', d.battery_discharge_current?.toFixed(1), 'A')}
            {row('PV input voltage', d.pv_input_voltage?.toFixed(1), 'V')}
            {row('PV input current', d.pv_input_current_for_battery?.toFixed(1), 'A')}
            {row('PV input power', Math.round(d.pv_input_power), 'W')}
            {row('Heat sink temperature', d.inverter_heat_sink_temperature?.toFixed(0), '°C')}
            {row('Charging on', d.is_charging_on ? 'yes' : 'no')}
            {row('Load on', d.is_load_on ? 'yes' : 'no')}
            {row('Switched on', d.is_switched_on ? 'yes' : 'no')}
          </tbody>
        </table>
      </div>
    </div>
  );
}
