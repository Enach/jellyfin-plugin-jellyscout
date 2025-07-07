using System;
using System.Net.Http;
using Jellyfin.Plugin.JellyScout.Services;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout;

/// <summary>
/// Simple service manager for JellyScout plugin services.
/// </summary>
public static class ServiceManager
{
    private static TMDBService? _tmdbService;
    private static StreamingService? _streamingService;
    private static NotificationService? _notificationService;
    private static JellyfinLibraryChecker? _libraryChecker;
    private static CatalogModule? _catalogModule;
    private static NotificationHub? _notificationHub;
    private static SonarrService? _sonarrService;
    private static RadarrService? _radarrService;
    private static CacheService? _cacheService;
    private static HealthCheckService? _healthCheckService;
    private static RetryService? _retryService;
    private static FilteringService? _filteringService;
    private static PlaylistService? _playlistService;
    private static ConfigurationValidationService? _configurationValidationService;
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// Gets the cache service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The cache service.</returns>
    public static CacheService GetCacheService(ILoggerFactory loggerFactory)
    {
        return _cacheService ??= new CacheService(loggerFactory.CreateLogger<CacheService>());
    }

    /// <summary>
    /// Gets the TMDB service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The TMDB service.</returns>
    public static TMDBService GetTMDBService(ILoggerFactory loggerFactory)
    {
        return _tmdbService ??= new TMDBService(_httpClient, loggerFactory.CreateLogger<TMDBService>(), GetCacheService(loggerFactory));
    }

    /// <summary>
    /// Gets the notification hub instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The notification hub.</returns>
    public static NotificationHub GetNotificationHub(ILoggerFactory loggerFactory)
    {
        return _notificationHub ??= new NotificationHub(loggerFactory.CreateLogger<NotificationHub>());
    }

    /// <summary>
    /// Gets the notification service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The notification service.</returns>
    public static NotificationService GetNotificationService(ILoggerFactory loggerFactory)
    {
        return _notificationService ??= new NotificationService(
            GetNotificationHub(loggerFactory),
            loggerFactory.CreateLogger<NotificationService>());
    }

    /// <summary>
    /// Gets the streaming service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The streaming service.</returns>
    public static StreamingService GetStreamingService(ILoggerFactory loggerFactory)
    {
        if (_streamingService == null)
        {
            _streamingService = new StreamingService(
                _httpClient,
                loggerFactory.CreateLogger<StreamingService>(),
                GetTMDBService(loggerFactory),
                GetNotificationService(loggerFactory));

            // Set up Sonarr and Radarr services
            _streamingService.SetSonarrService(GetSonarrService(loggerFactory));
            _streamingService.SetRadarrService(GetRadarrService(loggerFactory));
        }

        return _streamingService;
    }

    /// <summary>
    /// Gets the Sonarr service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The Sonarr service.</returns>
    public static SonarrService GetSonarrService(ILoggerFactory loggerFactory)
    {
        return _sonarrService ??= new SonarrService(_httpClient, loggerFactory.CreateLogger<SonarrService>());
    }

    /// <summary>
    /// Gets the Radarr service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The Radarr service.</returns>
    public static RadarrService GetRadarrService(ILoggerFactory loggerFactory)
    {
        return _radarrService ??= new RadarrService(_httpClient, loggerFactory.CreateLogger<RadarrService>());
    }

    /// <summary>
    /// Gets the health check service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The health check service.</returns>
    public static HealthCheckService GetHealthCheckService(ILoggerFactory loggerFactory)
    {
        return _healthCheckService ??= new HealthCheckService(_httpClient, loggerFactory.CreateLogger<HealthCheckService>(), GetCacheService(loggerFactory));
    }

    /// <summary>
    /// Gets the retry service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The retry service.</returns>
    public static RetryService GetRetryService(ILoggerFactory loggerFactory)
    {
        return _retryService ??= new RetryService(loggerFactory.CreateLogger<RetryService>());
    }

    /// <summary>
    /// Gets the filtering service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The filtering service.</returns>
    public static FilteringService GetFilteringService(ILoggerFactory loggerFactory)
    {
        return _filteringService ??= new FilteringService(loggerFactory.CreateLogger<FilteringService>());
    }

    /// <summary>
    /// Gets the playlist service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The playlist service.</returns>
    public static PlaylistService GetPlaylistService(ILoggerFactory loggerFactory)
    {
        return _playlistService ??= new PlaylistService(loggerFactory.CreateLogger<PlaylistService>());
    }

    /// <summary>
    /// Gets the configuration validation service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The configuration validation service.</returns>
    public static ConfigurationValidationService GetConfigurationValidationService(ILoggerFactory loggerFactory)
    {
        return _configurationValidationService ??= new ConfigurationValidationService(
            loggerFactory.CreateLogger<ConfigurationValidationService>(), 
            _httpClient, 
            GetCacheService(loggerFactory));
    }

    /// <summary>
    /// Gets the library checker instance.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The library checker.</returns>
    public static JellyfinLibraryChecker GetLibraryChecker(ILibraryManager libraryManager, ILoggerFactory loggerFactory)
    {
        return _libraryChecker ??= new JellyfinLibraryChecker(libraryManager, loggerFactory);
    }

    /// <summary>
    /// Gets the catalog module instance.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>The catalog module.</returns>
    public static CatalogModule GetCatalogModule(ILibraryManager libraryManager, ILoggerFactory loggerFactory)
    {
        return _catalogModule ??= new CatalogModule(
            loggerFactory.CreateLogger<CatalogModule>(),
            GetTMDBService(loggerFactory),
            GetLibraryChecker(libraryManager, loggerFactory),
            GetStreamingService(loggerFactory),
            GetNotificationService(loggerFactory),
            libraryManager);
    }

    /// <summary>
    /// Disposes all managed services.
    /// </summary>
    public static void Dispose()
    {
        _httpClient?.Dispose();
        _tmdbService = null;
        _streamingService = null;
        _notificationService = null;
        _libraryChecker = null;
        _catalogModule = null;
        _notificationHub = null;
        _sonarrService = null;
        _radarrService = null;
        _cacheService = null;
        _healthCheckService = null;
        _retryService = null;
        _filteringService = null;
        _playlistService = null;
        _configurationValidationService = null;
    }
} 