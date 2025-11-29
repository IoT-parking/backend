namespace backend.DTOs;

/// <summary>
/// DTO for MQTT message from sensor
/// </summary>
public class SensorMessageDto
{
    public string SensorType { get; set; } = string.Empty;
    public string SensorInstanceId { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
}
