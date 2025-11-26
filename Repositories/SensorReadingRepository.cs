using backend.Configuration;
using backend.DTOs;
using backend.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace backend.Repositories;

/// <summary>
/// Repository implementation for sensor readings in MongoDB
/// </summary>
public class SensorReadingRepository : ISensorReadingRepository
{
    private readonly IMongoCollection<SensorReading> _collection;

    public SensorReadingRepository(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        var database = client.GetDatabase(settings.Value.DatabaseName);
        _collection = database.GetCollection<SensorReading>(settings.Value.SensorReadingsCollectionName);

        // Create indexes for performance
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeysDefinition = Builders<SensorReading>.IndexKeys
            .Ascending(x => x.SensorType)
            .Ascending(x => x.SensorInstanceId)
            .Descending(x => x.Timestamp);

        var indexModel = new CreateIndexModel<SensorReading>(indexKeysDefinition);
        _collection.Indexes.CreateOneAsync(indexModel);
    }

    public async Task AddAsync(SensorReading reading)
    {
        await _collection.InsertOneAsync(reading);
    }

    public async Task AddManyAsync(IEnumerable<SensorReading> readings)
    {
        await _collection.InsertManyAsync(readings);
    }

    public async Task<PagedResponse<SensorReading>> GetAllAsync(int pageNumber, int pageSize)
    {
        var filter = Builders<SensorReading>.Filter.Empty;
        var totalRecords = await _collection.CountDocumentsAsync(filter);

        var data = await _collection
            .Find(filter)
            .SortByDescending(x => x.Timestamp)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PagedResponse<SensorReading>
        {
            Data = data,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords
        };
    }

    public async Task<PagedResponse<SensorReading>> GetFilteredAsync(SensorFilterParameters parameters)
    {
        var filterBuilder = Builders<SensorReading>.Filter;
        var filters = new List<FilterDefinition<SensorReading>>();

        // Sensor type filter
        if (!string.IsNullOrEmpty(parameters.SensorType))
        {
            filters.Add(filterBuilder.Eq(x => x.SensorType, parameters.SensorType));
        }

        // Sensor instance filter
        if (!string.IsNullOrEmpty(parameters.SensorInstanceId))
        {
            filters.Add(filterBuilder.Eq(x => x.SensorInstanceId, parameters.SensorInstanceId));
        }

        // Start date filter
        if (parameters.StartDate.HasValue)
        {
            filters.Add(filterBuilder.Gte(x => x.Timestamp, parameters.StartDate.Value.ToUniversalTime()));
        }

        // End date filter
        if (parameters.EndDate.HasValue)
        {
            filters.Add(filterBuilder.Lte(x => x.Timestamp, parameters.EndDate.Value.ToUniversalTime()));
        }

        var filter = filters.Any() ? filterBuilder.And(filters) : filterBuilder.Empty;
        var totalRecords = await _collection.CountDocumentsAsync(filter);

        // Sorting
        var sortDefinition = GetSortDefinition(parameters.SortBy, parameters.SortOrder);

        var data = await _collection
            .Find(filter)
            .Sort(sortDefinition)
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Limit(parameters.PageSize)
            .ToListAsync();

        return new PagedResponse<SensorReading>
        {
            Data = data,
            PageNumber = parameters.PageNumber,
            PageSize = parameters.PageSize,
            TotalRecords = totalRecords
        };
    }

    private SortDefinition<SensorReading> GetSortDefinition(string sortBy, string sortOrder)
    {
        var sortBuilder = Builders<SensorReading>.Sort;
        var ascending = sortOrder.ToLower() == "asc";

        return sortBy.ToLower() switch
        {
            "value" => ascending ? sortBuilder.Ascending(x => x.Value) : sortBuilder.Descending(x => x.Value),
            "sensortype" => ascending ? sortBuilder.Ascending(x => x.SensorType) : sortBuilder.Descending(x => x.SensorType),
            "sensorinstanceid" => ascending ? sortBuilder.Ascending(x => x.SensorInstanceId) : sortBuilder.Descending(x => x.SensorInstanceId),
            "timestamp" or _ => ascending ? sortBuilder.Ascending(x => x.Timestamp) : sortBuilder.Descending(x => x.Timestamp)
        };
    }

    public async Task<SensorReading?> GetByIdAsync(string id)
    {
        var filter = Builders<SensorReading>.Filter.Eq(x => x.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SensorReading>> GetLastNReadingsAsync(string sensorInstanceId, int count)
    {
        var filter = Builders<SensorReading>.Filter.Eq(x => x.SensorInstanceId, sensorInstanceId);
        return await _collection
            .Find(filter)
            .SortByDescending(x => x.Timestamp)
            .Limit(count)
            .ToListAsync();
    }

    public async Task<double?> GetAverageOfLastNReadingsAsync(string sensorInstanceId, int count)
    {
        var readings = await GetLastNReadingsAsync(sensorInstanceId, count);
        var readingsList = readings.ToList();
        
        if (!readingsList.Any())
            return null;

        return readingsList.Average(x => x.Value);
    }

    public async Task DeleteAllAsync()
    {
        var filter = Builders<SensorReading>.Filter.Empty;
        await _collection.DeleteManyAsync(filter);
    }

    public async Task<IEnumerable<string>> GetUniqueSensorTypesAsync()
    {
        var result = await _collection.DistinctAsync<string>("sensorType", FilterDefinition<SensorReading>.Empty);
        return await result.ToListAsync();
    }

    public async Task<IEnumerable<string>> GetUniqueSensorInstancesAsync(string? sensorType = null)
    {
        var filter = string.IsNullOrEmpty(sensorType)
            ? Builders<SensorReading>.Filter.Empty
            : Builders<SensorReading>.Filter.Eq(x => x.SensorType, sensorType);

        var result = await _collection.DistinctAsync<string>("sensorInstanceId", filter);
        return await result.ToListAsync();
    }
}
