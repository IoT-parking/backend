using System.Globalization;
using System.Text;
using System.Text.Json;
using backend.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace backend.Services;

/// <summary>
/// Service for exporting data in CSV and JSON formats
/// </summary>
public class ExportService : IExportService
{
    public byte[] ExportToCsv(IEnumerable<SensorReading> data)
    {
        using var memoryStream = new MemoryStream();
        using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8);
        using var csvWriter = new CsvWriter(streamWriter, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        });

        var exportData = data.Select(x => new
        {
            x.SensorType,
            x.SensorInstanceId,
            x.Value,
            x.Unit,
            Timestamp = x.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            x.Location
        });

        csvWriter.WriteRecords(exportData);
        streamWriter.Flush();
        return memoryStream.ToArray();
    }

    public byte[] ExportToJson(IEnumerable<SensorReading> data)
    {
        var exportData = data.Select(x => new
        {
            x.SensorType,
            x.SensorInstanceId,
            x.Value,
            x.Unit,
            x.Timestamp,
            x.Location
        });

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonString = JsonSerializer.Serialize(exportData, jsonOptions);
        return Encoding.UTF8.GetBytes(jsonString);
    }
}
