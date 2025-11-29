namespace backend.DTOs;

/// <summary>
/// DTO for sensor reading (API response)
/// </summary>
public class SensorReadingDto
{
    public string Id { get; set; } = string.Empty;
    public string SensorType { get; set; } = string.Empty;
    public string SensorInstanceId { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
