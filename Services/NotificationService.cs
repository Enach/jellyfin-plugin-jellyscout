using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for handling notifications.
/// </summary>
public class NotificationService
{
    private readonly NotificationHub _notificationHub;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    /// <param name="notificationHub">The notification hub.</param>
    /// <param name="logger">The logger.</param>
    public NotificationService(NotificationHub notificationHub, ILogger<NotificationService> logger)
    {
        _notificationHub = notificationHub;
        _logger = logger;
    }

    /// <summary>
    /// Sends a notification to all connected clients.
    /// </summary>
    /// <param name="type">The notification type.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="data">Additional data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendNotificationAsync(string type, string message, object? data = null)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            _logger.LogWarning("Notification type is required");
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Notification message is required");
            return;
        }

        try
        {
            await _notificationHub.SendNotificationAsync(type, message, data);
            _logger.LogInformation("Notification sent: {Type} - {Message}", type, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification: {Type} - {Message}", type, message);
        }
    }

    /// <summary>
    /// Sends a download ready notification.
    /// </summary>
    /// <param name="title">The media title.</param>
    /// <param name="year">The release year.</param>
    /// <param name="downloadPath">The download path.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendDownloadReadyNotificationAsync(string title, int? year, string? downloadPath = null)
    {
        var data = new
        {
            Title = title,
            Year = year,
            DownloadPath = downloadPath
        };

        await SendNotificationAsync("DownloadReady", $"Download ready: {title} ({year})", data);
    }

    /// <summary>
    /// Sends a streaming ready notification.
    /// </summary>
    /// <param name="title">The media title.</param>
    /// <param name="year">The release year.</param>
    /// <param name="streamingUrl">The streaming URL.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendStreamingReadyNotificationAsync(string title, int? year, string streamingUrl)
    {
        var data = new
        {
            Title = title,
            Year = year,
            StreamingUrl = streamingUrl
        };

        await SendNotificationAsync("StreamingReady", $"Streaming ready: {title} ({year})", data);
    }

    /// <summary>
    /// Sends an error notification.
    /// </summary>
    /// <param name="title">The media title.</param>
    /// <param name="error">The error message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendErrorNotificationAsync(string title, string error)
    {
        var data = new
        {
            Title = title,
            Error = error
        };

        await SendNotificationAsync("Error", $"Error with {title}: {error}", data);
    }

    /// <summary>
    /// Sends a progress notification.
    /// </summary>
    /// <param name="title">The media title.</param>
    /// <param name="progress">The progress percentage.</param>
    /// <param name="status">The status message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendProgressNotificationAsync(string title, double progress, string status)
    {
        var data = new
        {
            Title = title,
            Progress = progress,
            Status = status
        };

        await SendNotificationAsync("Progress", $"{title}: {status} ({progress:F1}%)", data);
    }
} 