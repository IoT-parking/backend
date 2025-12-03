using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

/// <summary>
/// SignalR hub for broadcasting real-time sensor data to connected clients
/// </summary>
public class SensorHub : Hub
{
    private readonly ILogger<SensorHub> _logger;

    public SensorHub(ILogger<SensorHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
