using MediaBrowser.Model.Plugins;
using System.ComponentModel.DataAnnotations;

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
    public bool EnableStreaming { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable download functionality.
    /// </summary>
    public bool EnableDownloads { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of search results to return.
    /// </summary>
    public int MaxSearchResults { get; set; } = 50;

    /// <summary>
    /// Gets or sets the default streaming quality preference.
    /// </summary>
    public string DefaultQuality { get; set; } = "1080p";

    /// <summary>
    /// Gets or sets a value indicating whether to enable notifications.
    /// </summary>
    public bool EnableNotifications { get; set; } = false;

    /// <summary>
    /// Gets or sets the Sonarr server URL.
    /// </summary>
    public string SonarrServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Sonarr API key.
    /// </summary>
    public string SonarrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Radarr server URL.
    /// </summary>
    public string RadarrServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Radarr API key.
    /// </summary>
    public string RadarrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Prowlarr server URL.
    /// </summary>
    public string ProwlarrServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Prowlarr API key.
    /// </summary>
    public string ProwlarrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum number of seeders for streaming.
    /// </summary>
    public int MinSeeders { get; set; } = 10;

    /// <summary>
    /// Gets or sets the BitPlay server URL.
    /// </summary>
    public string BitPlayServerUrl { get; set; } = "http://localhost:3347";

    // Properties needed by existing services to prevent build errors
    public bool AutoCheckLibrary { get; set; } = true;
    public int CacheExpirationMinutes { get; set; } = 60;
    public int MaxConcurrentRequests { get; set; } = 10;
    public bool EnableRateLimiting { get; set; } = true;
    public int RequestsPerSecond { get; set; } = 10;

    // Backward compatibility properties that populate from flat properties
    public SonarrConfiguration SonarrConfig => new()
    {
        Enabled = SonarrEnabled,
        ServerUrl = SonarrServerUrl,
        ApiKey = SonarrApiKey,
        TimeoutSeconds = 30,
        QualityProfileId = 1,
        QualityProfile = "HD-1080p"
    };

    public RadarrConfiguration RadarrConfig => new()
    {
        Enabled = RadarrEnabled,
        ServerUrl = RadarrServerUrl,
        ApiKey = RadarrApiKey,
        TimeoutSeconds = 30,
        QualityProfileId = 1,
        QualityProfile = "HD-1080p"
    };

    public ProwlarrConfiguration ProwlarrConfig => new()
    {
        Enabled = ProwlarrEnabled,
        ServerUrl = ProwlarrServerUrl,
        ApiKey = ProwlarrApiKey,
        MinSeeders = MinSeeders
    };

    public BitPlayConfiguration BitPlayConfig => new()
    {
        Enabled = BitPlayEnabled,
        ServerUrl = BitPlayServerUrl,
        StreamingTimeout = 30
    };

    // Aliases for backward compatibility
    public SonarrConfiguration Sonarr => SonarrConfig;
    public RadarrConfiguration Radarr => RadarrConfig;

    // Helper properties
    public bool SonarrEnabled => !string.IsNullOrEmpty(SonarrServerUrl) && !string.IsNullOrEmpty(SonarrApiKey);
    public bool RadarrEnabled => !string.IsNullOrEmpty(RadarrServerUrl) && !string.IsNullOrEmpty(RadarrApiKey);
    public bool ProwlarrEnabled => !string.IsNullOrEmpty(ProwlarrServerUrl) && !string.IsNullOrEmpty(ProwlarrApiKey);
    public bool BitPlayEnabled => !string.IsNullOrEmpty(BitPlayServerUrl);
}

/// <summary>
/// Configuration for Sonarr integration.
/// </summary>
public class SonarrConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether Sonarr is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the Sonarr server URL.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

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
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the Radarr server URL.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

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

/// <summary>
/// Configuration for Prowlarr integration.
/// </summary>
public class ProwlarrConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether Prowlarr is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the server URL.
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum seeders required for streaming.
    /// </summary>
    public int MinSeeders { get; set; } = 10;
}

/// <summary>
/// Configuration for BitPlay integration.
/// </summary>
public class BitPlayConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether BitPlay is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the server URL.
    /// </summary>
    public string ServerUrl { get; set; } = "http://localhost:3347";

    /// <summary>
    /// Gets or sets the streaming timeout in seconds.
    /// </summary>
    public int StreamingTimeout { get; set; } = 30;
} 