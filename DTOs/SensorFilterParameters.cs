namespace backend.DTOs;

/// <summary>
/// Filtering and sorting parameters for API
/// </summary>
public class SensorFilterParameters
{
    /// <summary>
    /// Sensor type filter
    /// </summary>
    public string? SensorType { get; set; }

    /// <summary>
    /// Sensor instance filter
    /// </summary>
    public string? SensorInstanceId { get; set; }

    /// <summary>
    /// Start date (inclusive)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date (inclusive)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Field to sort by (timestamp, value, sensorType, sensorInstanceId)
    /// </summary>
    public string SortBy { get; set; } = "timestamp";

    /// <summary>
    /// Sort direction (asc, desc)
    /// </summary>
    public string SortOrder { get; set; } = "desc";

    /// <summary>
    /// Page number (pagination)
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Page size (pagination)
    /// </summary>
    public int PageSize { get; set; } = 100;
}
