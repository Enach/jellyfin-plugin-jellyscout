using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.JellyScout.Models;

/// <summary>
/// Represents a torrent search result.
/// </summary>
public class TorrentResult
{
    /// <summary>
    /// Gets or sets the torrent name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the magnet link.
    /// </summary>
    public string MagnetLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size.
    /// </summary>
    public string Size { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of seeders.
    /// </summary>
    public int Seeders { get; set; }

    /// <summary>
    /// Gets or sets the number of leechers.
    /// </summary>
    public int Leechers { get; set; }

    /// <summary>
    /// Gets or sets the quality.
    /// </summary>
    public string Quality { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider/source.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the upload date.
    /// </summary>
    public DateTime? UploadDate { get; set; }

    /// <summary>
    /// Gets or sets the download URL.
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Gets or sets the info hash.
    /// </summary>
    public string? InfoHash { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the trusted status.
    /// </summary>
    public bool IsTrusted { get; set; }

    /// <summary>
    /// Gets or sets the VIP status.
    /// </summary>
    public bool IsVip { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
} 