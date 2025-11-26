using backend.Models;

namespace backend.Services;

/// <summary>
/// Interface for data export service
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports data to CSV format
    /// </summary>
    byte[] ExportToCsv(IEnumerable<SensorReading> data);

    /// <summary>
    /// Exports data to JSON format
    /// </summary>
    byte[] ExportToJson(IEnumerable<SensorReading> data);
}
