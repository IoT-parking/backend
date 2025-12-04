using System.Text;
using System.Text.Json;
using backend.Configuration;
using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;

namespace backend.Services;

/// <summary>
/// Background service for handling MQTT connection and saving data to MongoDB
/// </summary>
public class MqttClientService : BackgroundService
{
    private readonly ILogger<MqttClientService> _logger;
    private readonly MqttSettings _mqttSettings;
    private readonly ISensorReadingRepository _repository;
    private readonly IHubContext<SensorHub> _hubContext;
    private readonly BlockchainService _blockchainService;
    private IMqttClient? _mqttClient;

    public MqttClientService(
        ILogger<MqttClientService> logger,
        IOptions<MqttSettings> mqttSettings,
        ISensorReadingRepository repository,
        IHubContext<SensorHub> hubContext,
        BlockchainService blockchainService)
    {
        _logger = logger;
        _mqttSettings = mqttSettings.Value;
        _repository = repository;
        _hubContext = hubContext;
        _blockchainService = blockchainService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MQTT Client Service is starting...");

        // Create MQTT client
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        // Configure connection options
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqttSettings.BrokerHost, _mqttSettings.BrokerPort)
            .WithClientId(_mqttSettings.ClientId)
            .WithCleanSession();

        // Add authentication if configured
        if (!string.IsNullOrEmpty(_mqttSettings.Username) && !string.IsNullOrEmpty(_mqttSettings.Password))
        {
            optionsBuilder.WithCredentials(_mqttSettings.Username, _mqttSettings.Password);
        }

        // Add TLS if required
        if (_mqttSettings.UseTls)
        {
            optionsBuilder.WithTlsOptions(o => o.UseTls());
        }

        var options = optionsBuilder.Build();

        // Handle received messages
        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            await HandleMessageAsync(e);
        };

        // Handle disconnection
        _mqttClient.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("Disconnected from MQTT broker. Reason: {Reason}", e.Reason);
            
            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Attempting to reconnect to MQTT broker...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                
                try
                {
                    await _mqttClient.ConnectAsync(options, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reconnect to MQTT broker");
                }
            }
        };

        // Connect to broker
        try
        {
            await _mqttClient.ConnectAsync(options, stoppingToken);
            _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", _mqttSettings.BrokerHost, _mqttSettings.BrokerPort);

            // Subscribe to topic
            var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(_mqttSettings.Topic))
                .Build();

            await _mqttClient.SubscribeAsync(subscribeOptions, stoppingToken);
            _logger.LogInformation("Subscribed to topic: {Topic}", _mqttSettings.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MQTT broker");
        }

        // Keep service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            var topic = e.ApplicationMessage.Topic;
            
            _logger.LogDebug("Received message on topic {Topic}: {Payload}", topic, payload);

            // Parse topic: parking/sensor/{sensor_type}/{sensor_name}
            var topicParts = topic.Split('/');
            if (topicParts.Length != 4 || topicParts[0] != "parking" || topicParts[1] != "sensor")
            {
                _logger.LogWarning("Invalid topic format: {Topic}", topic);
                return;
            }

            var sensorType = topicParts[2];
            var sensorInstanceId = topicParts[3];

            // Parse value (can be int or float)
            if (!double.TryParse(payload, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                _logger.LogWarning("Failed to parse payload as number: {Payload}", payload);
                return;
            }

            // Get unit for given sensor type
            var unit = SensorType.GetUnit(sensorType);

            // Map to database model
            var sensorReading = new SensorReading
            {
                SensorType = sensorType,
                SensorInstanceId = sensorInstanceId,
                Value = value,
                Unit = unit,
                Timestamp = DateTime.UtcNow
            };

            // Save to database
            await _repository.AddAsync(sensorReading);
            _logger.LogInformation(
                "Saved reading from {SensorType}/{SensorInstanceId}: {Value} {Unit}",
                sensorReading.SensorType,
                sensorReading.SensorInstanceId,
                sensorReading.Value,
                sensorReading.Unit
            );

            // Broadcast to all connected SignalR clients
            await _hubContext.Clients.All.SendAsync("ReceiveSensorReading", new
            {
                sensorType = sensorReading.SensorType,
                sensorInstanceId = sensorReading.SensorInstanceId,
                value = sensorReading.Value,
                unit = sensorReading.Unit,
                timestamp = sensorReading.Timestamp
            });
            
            _logger.LogDebug("Broadcasted sensor reading to SignalR clients");
            _logger.LogInformation($"Inicjowanie nagrody blockchain dla {sensorInstanceId}...");
            _ = _blockchainService.RewardSensorAsync(sensorInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MQTT message from topic {Topic}", e.ApplicationMessage.Topic);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT Client Service is stopping...");

        if (_mqttClient != null && _mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
        }

        _mqttClient?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
