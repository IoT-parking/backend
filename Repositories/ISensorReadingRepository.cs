using backend.DTOs;
using backend.Models;

namespace backend.Repositories;

/// <summary>
/// Interface for sensor readings repository
/// </summary>
public interface ISensorReadingRepository
{
    /// <summary>
    /// Adds new reading to database
    /// </summary>
    Task AddAsync(SensorReading reading);

    /// <summary>
    /// Adds multiple readings to database
    /// </summary>
    Task AddManyAsync(IEnumerable<SensorReading> readings);

    /// <summary>
    /// Gets all readings with pagination
    /// </summary>
    Task<PagedResponse<SensorReading>> GetAllAsync(int pageNumber, int pageSize);

    /// <summary>
    /// Gets readings with filtering and sorting
    /// </summary>
    Task<PagedResponse<SensorReading>> GetFilteredAsync(SensorFilterParameters parameters);

    /// <summary>
    /// Gets reading by ID
    /// </summary>
    Task<SensorReading?> GetByIdAsync(string id);

    /// <summary>
    /// Gets last N readings for given sensor instance
    /// </summary>
    Task<IEnumerable<SensorReading>> GetLastNReadingsAsync(string sensorInstanceId, int count);

    /// <summary>
    /// Gets average value from last N readings for given sensor instance
    /// </summary>
    Task<double?> GetAverageOfLastNReadingsAsync(string sensorInstanceId, int count);

    /// <summary>
    /// Deletes all readings (for testing)
    /// </summary>
    Task DeleteAllAsync();

    /// <summary>
    /// Gets list of unique sensor types
    /// </summary>
    Task<IEnumerable<string>> GetUniqueSensorTypesAsync();

    /// <summary>
    /// Gets list of unique sensor instances
    /// </summary>
    Task<IEnumerable<string>> GetUniqueSensorInstancesAsync(string? sensorType = null);
}
