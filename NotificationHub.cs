using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout;

/// <summary>
/// SignalR hub for JellyScout real-time notifications.
/// </summary>
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to JellyScout notifications: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnect.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnDisconnectedAsync(System.Exception? exception)
    {
        _logger.LogInformation("Client disconnected from JellyScout notifications: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sends a notification to all connected clients.
    /// </summary>
    /// <param name="type">The notification type.</param>
    /// <param name="message">The message.</param>
    /// <param name="data">Additional data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendNotificationAsync(string type, string message, object? data = null)
    {
        _logger.LogInformation("Sending notification: {Type} - {Message}", type, message);
        
        try
        {
            // Send to all connected clients
            await Clients.All.SendAsync("Notification", new { Type = type, Message = message, Data = data });

            // Send specific notification types for better client handling
            switch (type)
            {
                case "DownloadReady":
                    await Clients.All.SendAsync("DownloadReady", data);
                    break;
                case "StreamingReady":
                case "Stream Ready":
                    await Clients.All.SendAsync("StreamingReady", data);
                    break;
                case "Error":
                    await Clients.All.SendAsync("Error", data);
                    break;
                case "Progress":
                    await Clients.All.SendAsync("Progress", data);
                    break;
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error sending notification: {Type} - {Message}", type, message);
        }
    }
}
