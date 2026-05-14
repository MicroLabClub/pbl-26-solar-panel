using MongoDB.Bson.Serialization.Attributes;

namespace SolarMonitoring.API.Models;

public class Problem
{
    [BsonId]
    [BsonElement("_id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("severity")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public AlertSeverity Severity { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("started_at")]
    public DateTime StartedAt { get; set; }

    [BsonElement("ended_at")]
    public DateTime? EndedAt { get; set; }

    [BsonElement("occurrences")]
    public int Occurrences { get; set; } = 1;
}

public class HistoryPoint
{
    public DateTime Hour { get; set; }
    public double ProducedPowerWatts { get; set; }
    public double ProducedEnergyWh { get; set; }
    public double LoadWatts { get; set; }
}

public class SystemCheck
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = "ok"; // ok | warn | critical
    public string Detail { get; set; } = string.Empty;
}
