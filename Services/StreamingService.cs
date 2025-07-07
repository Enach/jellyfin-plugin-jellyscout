using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyScout.Configuration;
using Jellyfin.Plugin.JellyScout.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for handling streaming and torrent operations using Sonarr, Radarr, Prowlarr, and BitPlay.
/// </summary>
public class StreamingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StreamingService> _logger;
    private readonly TMDBService _tmdbService;
    private readonly NotificationService _notificationService;

    private SonarrService? _sonarrService;
    private RadarrService? _radarrService;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="tmdbService">The TMDB service.</param>
    /// <param name="notificationService">The notification service.</param>
    public StreamingService(
        HttpClient httpClient, 
        ILogger<StreamingService> logger,
        TMDBService tmdbService,
        NotificationService notificationService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _tmdbService = tmdbService;
        _notificationService = notificationService;
        
        // Configure HttpClient timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }



    /// <summary>
    /// Searches for torrents based on media information using Sonarr/Radarr APIs.
    /// </summary>
    /// <param name="title">The media title.</param>
    /// <param name="year">The release year.</param>
    /// <param name="mediaType">The media type (movie/tv).</param>
    /// <param name="quality">The preferred quality.</param>
    /// <returns>A collection of torrent results.</returns>
    public async Task<IEnumerable<TorrentResult>> SearchTorrentsAsync(
        string title, 
        int? year, 
        string mediaType, 
        string quality = "1080p")
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            _logger.LogWarning("Title is required for torrent search");
            return Enumerable.Empty<TorrentResult>();
        }

        _logger.LogInformation("Searching torrents for: {Title} ({MediaType}) using Sonarr/Radarr", title, mediaType);

        try
        {
            // Use Sonarr for TV shows, Radarr for movies
            if (mediaType.Equals("tv", StringComparison.OrdinalIgnoreCase))
            {
                var sonarrService = GetSonarrService();
                if (sonarrService != null)
                {
                    // Note: Direct torrent search is now handled by Sonarr after adding the show
                    // The user should use the "Add to Sonarr" functionality instead
                    _logger.LogInformation("TV show detected, would use Sonarr integration");
                    return Enumerable.Empty<TorrentResult>();
                }
            }
            else // Default to movie search
            {
                var radarrService = GetRadarrService();
                if (radarrService != null)
                {
                    // Note: Direct torrent search is now handled by Radarr after adding the movie
                    // The user should use the "Add to Radarr" functionality instead
                    _logger.LogInformation("Movie detected, would use Radarr integration");
                    return Enumerable.Empty<TorrentResult>();
                }
            }

            _logger.LogWarning("No configured torrent search service available for {MediaType}", mediaType);
            return Enumerable.Empty<TorrentResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching torrents for {Title} ({MediaType})", title, mediaType);
            return Enumerable.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Sets the Sonarr service for TV show searching.
    /// </summary>
    /// <param name="sonarrService">The Sonarr service.</param>
    public void SetSonarrService(SonarrService sonarrService)
    {
        _sonarrService = sonarrService;
    }

    /// <summary>
    /// Sets the Radarr service for movie searching.
    /// </summary>
    /// <param name="radarrService">The Radarr service.</param>
    public void SetRadarrService(RadarrService radarrService)
    {
        _radarrService = radarrService;
    }

    private SonarrService? GetSonarrService()
    {
        return _sonarrService;
    }

    private RadarrService? GetRadarrService()
    {
        return _radarrService;
    }

    /// <summary>
    /// Initiates streaming for a torrent.
    /// </summary>
    /// <param name="magnetLink">The magnet link.</param>
    /// <param name="title">The media title.</param>
    /// <returns>The streaming URL.</returns>
    public async Task<string?> StartStreamingAsync(string magnetLink, string title)
    {
        if (string.IsNullOrWhiteSpace(magnetLink))
        {
            _logger.LogWarning("Magnet link is required for streaming");
            return null;
        }

        if (!IsValidMagnetLink(magnetLink))
        {
            _logger.LogWarning("Invalid magnet link format: {MagnetLink}", magnetLink);
            return null;
        }

        try
        {
            _logger.LogInformation("Starting stream for {Title}", title);

            // In a real implementation, this would integrate with a torrent streaming service
            // like WebTorrent, Stremio, or a custom torrent client
            var streamingUrl = await InitiateTorrentStreaming(magnetLink, title);

            if (!string.IsNullOrEmpty(streamingUrl))
            {
                await _notificationService.SendNotificationAsync(
                    "Stream Ready", 
                    $"Streaming is ready for {title}",
                    streamingUrl);
            }

            return streamingUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting stream for {Title}", title);
            return null;
        }
    }

    /// <summary>
    /// Initiates download for a torrent.
    /// </summary>
    /// <param name="magnetLink">The magnet link.</param>
    /// <param name="title">The media title.</param>
    /// <returns>True if download was initiated successfully.</returns>
    public async Task<bool> StartDownloadAsync(string magnetLink, string title)
    {
        if (string.IsNullOrWhiteSpace(magnetLink))
        {
            _logger.LogWarning("Magnet link is required for download");
            return false;
        }

        if (!IsValidMagnetLink(magnetLink))
        {
            _logger.LogWarning("Invalid magnet link format: {MagnetLink}", magnetLink);
            return false;
        }

        try
        {
            _logger.LogInformation("Starting download for {Title}", title);

            // In a real implementation, this would integrate with a download client
            // like qBittorrent, Transmission, or Deluge
            var success = await InitiateTorrentDownload(magnetLink, title);

            if (success)
            {
                await _notificationService.SendNotificationAsync(
                    "Download Started", 
                    $"Download started for {title}",
                    null);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting download for {Title}", title);
            return false;
        }
    }

    private static string BuildSearchQuery(string title, int? year, string mediaType, string quality)
    {
        var query = title.Trim();
        
        if (year.HasValue)
        {
            query += $" {year}";
        }

        if (!string.IsNullOrWhiteSpace(quality))
        {
            query += $" {quality}";
        }

        return query;
    }

    private static bool IsValidMagnetLink(string magnetLink)
    {
        return magnetLink.StartsWith("magnet:?xt=urn:btih:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidTorrent(TorrentResult torrent)
    {
        return !string.IsNullOrWhiteSpace(torrent.Name) &&
               !string.IsNullOrWhiteSpace(torrent.MagnetLink) &&
               torrent.Seeders > 0;
    }

    private static int GetQualityScore(string quality)
    {
        return quality?.ToLowerInvariant() switch
        {
            "4k" or "2160p" => 100,
            "1080p" => 90,
            "720p" => 80,
            "480p" => 70,
            _ => 50
        };
    }

    /// <summary>
    /// Initiates torrent streaming (replacement for Stremio integration).
    /// </summary>
    /// <param name="magnetLink">The magnet link.</param>
    /// <param name="title">The media title.</param>
    /// <returns>The streaming URL or null if failed.</returns>
    private async Task<string?> InitiateTorrentStreaming(string magnetLink, string title)
    {
        try
        {
            _logger.LogInformation("Starting torrent stream for: {Title}", title);

            // Option 1: Use a torrent streaming service like WebTorrent
            // This would require a WebTorrent server or similar
            var streamingUrl = await StartWebTorrentStream(magnetLink, title);
            
            if (!string.IsNullOrEmpty(streamingUrl))
            {
                return streamingUrl;
            }

            // Option 2: Use a local torrent client with streaming capability
            streamingUrl = await StartLocalTorrentStream(magnetLink, title);
            
            return streamingUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start torrent stream for: {Title}", title);
            return null;
        }
    }

    /// <summary>
    /// Starts streaming using WebTorrent or similar service.
    /// </summary>
    /// <param name="magnetLink">The magnet link.</param>
    /// <param name="title">The media title.</param>
    /// <returns>Streaming URL.</returns>
    private async Task<string?> StartWebTorrentStream(string magnetLink, string title)
    {
        try
        {
            // WebTorrent streaming is not currently configured
            // This would require a WebTorrent server to be running
            _logger.LogInformation("WebTorrent streaming is not currently available for: {Title}", title);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebTorrent streaming failed for: {Title}", title);
            return null;
        }
    }

    /// <summary>
    /// Starts streaming using local torrent client.
    /// </summary>
    /// <param name="magnetLink">The magnet link.</param>
    /// <param name="title">The media title.</param>
    /// <returns>Streaming URL.</returns>
    private async Task<string?> StartLocalTorrentStream(string magnetLink, string title)
    {
        try
        {
            // Option: Use qBittorrent with sequential download + HTTP server
            // 1. Add torrent to qBittorrent with sequential download
            // 2. Start download
            // 3. Return HTTP URL to the downloading file
            
            // First, try to add the torrent to the client
            var success = await AddToTorrentClient(magnetLink, title);
            
            if (success)
            {
                // Extract hash and return streaming URL
                var torrentHash = ExtractHashFromMagnet(magnetLink);
                if (!string.IsNullOrEmpty(torrentHash))
                {
                    // This would be the URL to stream from qBittorrent's HTTP interface
                    // (assuming qBittorrent is configured with Web UI and sequential download)
                    return $"http://localhost:8080/api/v2/torrents/files?hash={torrentHash}";
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local torrent streaming failed for: {Title}", title);
            return null;
        }
    }

    /// <summary>
    /// Initiates torrent download using your existing setup.
    /// </summary>
    /// <param name="magnetLink">The magnet link.</param>
    /// <param name="title">The media title.</param>
    /// <returns>True if download started successfully.</returns>
    private async Task<bool> InitiateTorrentDownload(string magnetLink, string title)
    {
        try
        {
            _logger.LogInformation("Starting torrent download for: {Title}", title);

            // Since you have Sonarr/Radarr working, use them instead of direct torrent client
            // This is just a fallback for manual magnet links

            // Option 1: Add to qBittorrent/Transmission directly
            var success = await AddToTorrentClient(magnetLink, title);
            
            if (success)
            {
                await _notificationService.SendNotificationAsync(
                    "Download Started", 
                    $"Started downloading: {title}",
                    new { magnetLink, title });
                
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start torrent download for: {Title}", title);
            return false;
        }
    }

    /// <summary>
    /// Adds torrent to configured torrent client.
    /// </summary>
    /// <param name="magnetLink">The magnet link.</param>
    /// <param name="title">The media title.</param>
    /// <returns>True if added successfully.</returns>
    private async Task<bool> AddToTorrentClient(string magnetLink, string title)
    {
        try
        {
            // Example: qBittorrent Web API
            // POST /api/v2/torrents/add
            var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(magnetLink), "urls");
            formData.Add(new StringContent("true"), "sequentialDownload"); // For streaming

            var response = await _httpClient.PostAsync(
                "http://localhost:8080/api/v2/torrents/add", 
                formData);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add torrent to client: {Error}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Extracts hash from magnet link.
    /// </summary>
    /// <param name="magnetLink">The magnet link.</param>
    /// <returns>The torrent hash.</returns>
    private static string? ExtractHashFromMagnet(string magnetLink)
    {
        try
        {
            var match = Regex.Match(magnetLink, @"xt=urn:btih:([a-fA-F0-9]{40})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }
}

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
    /// Gets or sets the provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the upload date.
    /// </summary>
    public DateTime? UploadDate { get; set; }
}

 