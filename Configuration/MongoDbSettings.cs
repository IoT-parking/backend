namespace backend.Configuration;

/// <summary>
/// MongoDB database connection settings
/// Values are read from environment variables or appsettings.json
/// </summary>
public class MongoDbSettings
{
    public const string SectionName = "MongoDbSettings";

    /// <summary>
    /// Connection string to MongoDB (can contain username and password)
    /// Env variable: MONGODB_CONNECTION_STRING
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Database name
    /// Env variable: MONGODB_DATABASE_NAME
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Collection name for sensor readings
    /// Env variable: MONGODB_COLLECTION_NAME
    /// </summary>
    public string SensorReadingsCollectionName { get; set; } = "sensor_readings";
}
