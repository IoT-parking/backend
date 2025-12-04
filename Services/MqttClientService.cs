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
/// Background service for handling MQTT connection, saving data to MongoDB and triggering Blockchain rewards
/// </summary>
public class MqttClientService : BackgroundService
{
    private readonly ILogger<MqttClientService> _logger;
    private readonly MqttSettings _mqttSettings;
    private readonly ISensorReadingRepository _repository;
    private readonly IHubContext<SensorHub> _hubContext;
    private readonly BlockchainService _blockchainService; // Wstrzyknięty serwis
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

        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqttSettings.BrokerHost, _mqttSettings.BrokerPort)
            .WithClientId(_mqttSettings.ClientId)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(_mqttSettings.Username) && !string.IsNullOrEmpty(_mqttSettings.Password))
        {
            optionsBuilder.WithCredentials(_mqttSettings.Username, _mqttSettings.Password);
        }

        if (_mqttSettings.UseTls)
        {
            optionsBuilder.WithTlsOptions(o => o.UseTls());
        }

        var options = optionsBuilder.Build();

        _mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            await HandleMessageAsync(e);
        };

        _mqttClient.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("Disconnected from MQTT broker. Reason: {Reason}", e.Reason);
            
            if (!stoppingToken.IsCancellationRequested)
            {
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

        try
        {
            await _mqttClient.ConnectAsync(options, stoppingToken);
            _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", _mqttSettings.BrokerHost, _mqttSettings.BrokerPort);

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

            var topicParts = topic.Split('/');
            if (topicParts.Length != 4 || topicParts[0] != "parking" || topicParts[1] != "sensor")
            {
                _logger.LogWarning("Invalid topic format: {Topic}", topic);
                return;
            }

            var sensorType = topicParts[2];
            var sensorInstanceId = topicParts[3];

            if (!double.TryParse(payload, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                _logger.LogWarning("Failed to parse payload as number: {Payload}", payload);
                return;
            }

            var unit = SensorType.GetUnit(sensorType);

            var sensorReading = new SensorReading
            {
                SensorType = sensorType,
                SensorInstanceId = sensorInstanceId,
                Value = value,
                Unit = unit,
                Timestamp = DateTime.UtcNow
            };

            // 1. Zapisz do MongoDB
            await _repository.AddAsync(sensorReading);
            
            // 2. Wyślij przez SignalR do Frontendu (Live Data)
            await _hubContext.Clients.All.SendAsync("ReceiveSensorReading", new
            {
                sensorType = sensorReading.SensorType,
                sensorInstanceId = sensorReading.SensorInstanceId,
                value = sensorReading.Value,
                unit = sensorReading.Unit,
                timestamp = sensorReading.Timestamp
            });
            
            // 3. BLOCKCHAIN REWARD TRIGGER
            // Używamy wzorca "Fire and Forget" (_ = ...), aby nie blokować wątku MQTT oczekiwaniem na Blockchain
            _logger.LogInformation($"[MqttService] Triggering Blockchain Reward for: {sensorInstanceId}");
            
            _ = Task.Run(async () => 
            {
                try 
                {
                    await _blockchainService.RewardSensorAsync(sensorInstanceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[Blockchain Error] Failed to reward {sensorInstanceId}: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MQTT message from topic {Topic}", e.ApplicationMessage.Topic);
        }
    }
}