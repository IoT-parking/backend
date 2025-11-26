namespace backend.Configuration;

/// <summary>
/// MQTT broker connection settings
/// Values are read from environment variables or appsettings.json
/// </summary>
public class MqttSettings
{
    public const string SectionName = "MqttSettings";

    /// <summary>
    /// MQTT broker address
    /// Env variable: MQTT_BROKER_HOST
    /// </summary>
    public string BrokerHost { get; set; } = "localhost";

    /// <summary>
    /// MQTT broker port
    /// Env variable: MQTT_BROKER_PORT
    /// </summary>
    public int BrokerPort { get; set; } = 1883;

    /// <summary>
    /// MQTT username (optional)
    /// Env variable: MQTT_USERNAME
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// MQTT password (optional)
    /// Env variable: MQTT_PASSWORD
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// MQTT client identifier
    /// Env variable: MQTT_CLIENT_ID
    /// </summary>
    public string ClientId { get; set; } = "iot-parking-backend";

    /// <summary>
    /// MQTT topic to subscribe (supports wildcards)
    /// Env variable: MQTT_TOPIC
    /// </summary>
    public string Topic { get; set; } = "sensors/#";

    /// <summary>
    /// Whether to use TLS/SSL
    /// Env variable: MQTT_USE_TLS
    /// </summary>
    public bool UseTls { get; set; } = false;
}
