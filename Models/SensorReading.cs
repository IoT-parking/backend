using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models;

/// <summary>
/// Represents an IoT sensor reading stored in MongoDB database
/// </summary>
public class SensorReading
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Sensor type (e.g. "Temperature", "Humidity", "Gas", "UV")
    /// </summary>
    [BsonElement("sensorType")]
    [BsonRequired]
    public string SensorType { get; set; } = string.Empty;

    /// <summary>
    /// Sensor instance identifier (e.g. "sensor-001")
    /// </summary>
    [BsonElement("sensorInstanceId")]
    [BsonRequired]
    public string SensorInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Reading value
    /// </summary>
    [BsonElement("value")]
    [BsonRequired]
    public double Value { get; set; }

    /// <summary>
    /// Unit of measurement (e.g. "°C", "%", "ppm")
    /// </summary>
    [BsonElement("unit")]
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Reading registration time
    /// </summary>
    [BsonElement("timestamp")]
    [BsonRequired]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Sensor location (optional)
    /// </summary>
    [BsonElement("location")]
    public string? Location { get; set; }
}
