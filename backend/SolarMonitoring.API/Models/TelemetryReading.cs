using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarMonitoring.API.Models;

public class TelemetryReading
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }

    [BsonElement("source")]
    public string Source { get; set; } = "mpp-solar";

    [BsonElement("data")]
    public InverterData Data { get; set; } = new();
}

public class InverterData
{
    [BsonElement("_command")]
    public string? Command { get; set; }

    [BsonElement("_command_description")]
    public string? CommandDescription { get; set; }

    [BsonElement("ac_input_voltage")]
    public double AcInputVoltage { get; set; }

    [BsonElement("ac_input_frequency")]
    public double AcInputFrequency { get; set; }

    [BsonElement("ac_output_voltage")]
    public double AcOutputVoltage { get; set; }

    [BsonElement("ac_output_frequency")]
    public double AcOutputFrequency { get; set; }

    [BsonElement("ac_output_apparent_power")]
    public double AcOutputApparentPower { get; set; }

    [BsonElement("ac_output_active_power")]
    public double AcOutputActivePower { get; set; }

    [BsonElement("ac_output_load")]
    public double AcOutputLoad { get; set; }

    [BsonElement("bus_voltage")]
    public double BusVoltage { get; set; }

    [BsonElement("battery_voltage")]
    public double BatteryVoltage { get; set; }

    [BsonElement("battery_charging_current")]
    public double BatteryChargingCurrent { get; set; }

    [BsonElement("battery_capacity")]
    public double BatteryCapacity { get; set; }

    [BsonElement("inverter_heat_sink_temperature")]
    public double InverterHeatSinkTemperature { get; set; }

    [BsonElement("pv_input_current_for_battery")]
    public double PvInputCurrentForBattery { get; set; }

    [BsonElement("pv_input_voltage")]
    public double PvInputVoltage { get; set; }

    [BsonElement("battery_voltage_from_scc")]
    public double BatteryVoltageFromScc { get; set; }

    [BsonElement("battery_discharge_current")]
    public double BatteryDischargeCurrent { get; set; }

    [BsonElement("is_sbu_priority_version_added")]
    public int IsSbuPriorityVersionAdded { get; set; }

    [BsonElement("is_configuration_changed")]
    public int IsConfigurationChanged { get; set; }

    [BsonElement("is_scc_firmware_updated")]
    public int IsSccFirmwareUpdated { get; set; }

    [BsonElement("is_load_on")]
    public int IsLoadOn { get; set; }

    [BsonElement("is_battery_voltage_to_steady_while_charging")]
    public int IsBatteryVoltageToSteadyWhileCharging { get; set; }

    [BsonElement("is_charging_on")]
    public int IsChargingOn { get; set; }

    [BsonElement("is_scc_charging_on")]
    public int IsSccChargingOn { get; set; }

    [BsonElement("is_ac_charging_on")]
    public int IsAcChargingOn { get; set; }

    [BsonElement("rsv1")]
    public int Rsv1 { get; set; }

    [BsonElement("rsv2")]
    public int Rsv2 { get; set; }

    [BsonElement("pv_input_power")]
    public double PvInputPower { get; set; }

    [BsonElement("is_charging_to_float")]
    public int IsChargingToFloat { get; set; }

    [BsonElement("is_switched_on")]
    public int IsSwitchedOn { get; set; }

    [BsonElement("is_reserved")]
    public int IsReserved { get; set; }
}
