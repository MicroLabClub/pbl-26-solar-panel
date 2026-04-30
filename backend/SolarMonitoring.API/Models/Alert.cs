using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SolarMonitoring.API.Models;

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public class Alert
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("severity")]
    [BsonRepresentation(BsonType.String)]
    public AlertSeverity Severity { get; set; }

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("acknowledged")]
    public bool Acknowledged { get; set; } = false;

    [BsonElement("source_reading_id")]
    public string? SourceReadingId { get; set; }
}
