using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyScout.Configuration;

/// <summary>
/// Configuration for the JellyScout plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the TMDB API key.
    /// </summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to enable streaming functionality.
    /// </summary>
    public bool EnableStreaming { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable download functionality.
    /// </summary>
    public bool EnableDownloads { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of search results to return.
    /// </summary>
    public int MaxSearchResults { get; set; } = 50;

    /// <summary>
    /// Gets or sets the default streaming quality preference.
    /// </summary>
    public string DefaultQuality { get; set; } = "1080p";

    /// <summary>
    /// Gets or sets a value indicating whether to automatically check if items exist in the library.
    /// </summary>
    public bool AutoCheckLibrary { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for external API requests in seconds.
    /// </summary>
    public int ApiTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether to enable notifications.
    /// </summary>
    public bool EnableNotifications { get; set; } = true;

    /// <summary>
    /// Gets or sets the Streamio configuration.
    /// </summary>
    public StreamioConfiguration StreamioConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the Sonarr configuration.
    /// </summary>
    public SonarrConfiguration SonarrConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the Radarr configuration.
    /// </summary>
    public RadarrConfiguration RadarrConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the Streamio configuration (alias for StreamioConfig).
    /// </summary>
    public StreamioConfiguration Streamio => StreamioConfig;

    /// <summary>
    /// Gets or sets the Sonarr configuration (alias for SonarrConfig).
    /// </summary>
    public SonarrConfiguration Sonarr => SonarrConfig;

    /// <summary>
    /// Gets or sets the Radarr configuration (alias for RadarrConfig).
    /// </summary>
    public RadarrConfiguration Radarr => RadarrConfig;

    /// <summary>
    /// Gets or sets the cache expiration time in minutes.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of concurrent requests.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether rate limiting is enabled.
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum requests per second.
    /// </summary>
    public int RequestsPerSecond { get; set; } = 10;
}

/// <summary>
/// Configuration for Streamio integration.
/// </summary>
public class StreamioConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether Streamio is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the Streamio server URL.
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:11470";

    /// <summary>
    /// Gets or sets the Streamio API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether to use HTTPS.
    /// </summary>
    public bool UseHttps { get; set; } = false;
}

/// <summary>
/// Configuration for Sonarr integration.
/// </summary>
public class SonarrConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether Sonarr is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the Sonarr server URL.
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:8989";

    /// <summary>
    /// Gets or sets the Sonarr API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the quality profile ID.
    /// </summary>
    public int QualityProfileId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the quality profile name.
    /// </summary>
    public string QualityProfile { get; set; } = "HD-1080p";
}

/// <summary>
/// Configuration for Radarr integration.
/// </summary>
public class RadarrConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether Radarr is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the Radarr server URL.
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:7878";

    /// <summary>
    /// Gets or sets the Radarr API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the quality profile ID.
    /// </summary>
    public int QualityProfileId { get; set; } = 1;

    /// <summary>
    /// Gets or sets the quality profile name.
    /// </summary>
    public string QualityProfile { get; set; } = "HD-1080p";
} 