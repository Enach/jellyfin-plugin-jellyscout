using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.JellyScout.Models;

/// <summary>
/// Represents the download status of media.
/// </summary>
public class DownloadStatus
{
    /// <summary>
    /// Gets or sets the current status.
    /// </summary>
    public MediaStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the download progress (0-100).
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Gets or sets additional details about the download.
    /// </summary>
    public List<string> Details { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the last update.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Enumeration of possible media download statuses.
/// </summary>
public enum MediaStatus
{
    /// <summary>
    /// Media is not in the system.
    /// </summary>
    NotInSystem = 0,

    /// <summary>
    /// Media is added but not downloaded.
    /// </summary>
    Wanted = 1,

    /// <summary>
    /// Media is currently downloading.
    /// </summary>
    Downloading = 2,

    /// <summary>
    /// Media is fully downloaded.
    /// </summary>
    Downloaded = 3,

    /// <summary>
    /// Media is partially downloaded (for TV series).
    /// </summary>
    PartiallyDownloaded = 4,

    /// <summary>
    /// Media is added but not monitored.
    /// </summary>
    NotMonitored = 5,

    /// <summary>
    /// Download failed or error occurred.
    /// </summary>
    Failed = 6
} 