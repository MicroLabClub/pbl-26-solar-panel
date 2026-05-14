using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarMonitoring.API.Models;

public class Installation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }

    [BsonElement("timezone")]
    public string Timezone { get; set; } = "UTC";

    [BsonElement("system_capacity_watts")]
    public double SystemCapacityWatts { get; set; }

    [BsonElement("panel_tilt_deg")]
    public double PanelTiltDeg { get; set; } = 30;

    [BsonElement("panel_azimuth_deg")]
    public double PanelAzimuthDeg { get; set; } = 180;

    [BsonElement("mqtt_device_id")]
    public string MqttDeviceId { get; set; } = string.Empty;

    [BsonElement("notes")]
    public string Notes { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
