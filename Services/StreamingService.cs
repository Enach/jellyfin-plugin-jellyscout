using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyScout.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for handling streaming and torrent operations using Streamio.
/// </summary>
public class StreamingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StreamingService> _logger;
    private readonly TMDBService _tmdbService;
    private readonly NotificationService _notificationService;
    private StreamioConfiguration? _streamioConfig;
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
        
        // Configure HttpClient for Streamio
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Sets the Streamio configuration.
    /// </summary>
    /// <param name="config">The Streamio configuration.</param>
    public void SetStreamioConfiguration(StreamioConfiguration config)
    {
        _streamioConfig = config;
        _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
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
                    return await sonarrService.SearchTVShowTorrentsAsync(title, year, null, quality);
                }
            }
            else // Default to movie search
            {
                var radarrService = GetRadarrService();
                if (radarrService != null)
                {
                    return await radarrService.SearchMovieTorrentsAsync(title, year, quality);
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



    private async Task<string?> InitiateTorrentStreaming(string magnetLink, string title)
    {
        try
        {
            if (_streamioConfig == null || !_streamioConfig.Enabled)
            {
                _logger.LogWarning("Streamio is not configured or disabled");
                return null;
            }

            _logger.LogInformation("Initiating torrent streaming for {Title} using Streamio", title);

            // Create stream request for Streamio
            var streamRequest = new
            {
                magnet = magnetLink,
                title = title,
                autostart = true
            };

            var jsonContent = JsonConvert.SerializeObject(streamRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Add API key if configured
            if (!string.IsNullOrEmpty(_streamioConfig.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _streamioConfig.ApiKey);
            }

            var response = await _httpClient.PostAsync($"{_streamioConfig.ServerUrl}/api/stream", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var streamResponse = JsonConvert.DeserializeObject<StreamioStreamResponse>(responseContent);
                
                if (streamResponse != null && !string.IsNullOrEmpty(streamResponse.StreamUrl))
                {
                    _logger.LogInformation("Streamio streaming URL obtained: {StreamingUrl}", streamResponse.StreamUrl);
                    return streamResponse.StreamUrl;
                }
            }
            else
            {
                _logger.LogError("Streamio API error: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating torrent streaming for {Title}", title);
            return null;
        }
    }

    private async Task<bool> InitiateTorrentDownload(string magnetLink, string title)
    {
        try
        {
            if (_streamioConfig == null || !_streamioConfig.Enabled)
            {
                _logger.LogWarning("Streamio is not configured or disabled");
                return false;
            }

            _logger.LogInformation("Initiating torrent download for {Title} using Streamio", title);

            // Create download request for Streamio
            var downloadRequest = new
            {
                magnet = magnetLink,
                title = title,
                download = true,
                autostart = true
            };

            var jsonContent = JsonConvert.SerializeObject(downloadRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Add API key if configured
            if (!string.IsNullOrEmpty(_streamioConfig.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _streamioConfig.ApiKey);
            }

            var response = await _httpClient.PostAsync($"{_streamioConfig.ServerUrl}/api/download", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var downloadResponse = JsonConvert.DeserializeObject<StreamioDownloadResponse>(responseContent);
                
                if (downloadResponse != null && downloadResponse.Success)
                {
                    _logger.LogInformation("Streamio download initiated successfully for {Title}", title);
                    return true;
                }
            }
            else
            {
                _logger.LogError("Streamio download API error: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating torrent download for {Title}", title);
            return false;
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

/// <summary>
/// Response model for Streamio streaming requests.
/// </summary>
public class StreamioStreamResponse
{
    /// <summary>
    /// Gets or sets the streaming URL.
    /// </summary>
    [JsonProperty("stream_url")]
    public string StreamUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the request was successful.
    /// </summary>
    [JsonProperty("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if any.
    /// </summary>
    [JsonProperty("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the torrent info hash.
    /// </summary>
    [JsonProperty("info_hash")]
    public string? InfoHash { get; set; }
}

/// <summary>
/// Response model for Streamio download requests.
/// </summary>
public class StreamioDownloadResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the request was successful.
    /// </summary>
    [JsonProperty("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the download ID.
    /// </summary>
    [JsonProperty("download_id")]
    public string? DownloadId { get; set; }

    /// <summary>
    /// Gets or sets the error message if any.
    /// </summary>
    [JsonProperty("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the torrent info hash.
    /// </summary>
    [JsonProperty("info_hash")]
    public string? InfoHash { get; set; }
} 