using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyScout.Services;
using Jellyfin.Plugin.JellyScout.Configuration;
using Jellyfin.Plugin.JellyScout.Models;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using static Jellyfin.Plugin.JellyScout.Services.HealthCheckService;

namespace Jellyfin.Plugin.JellyScout.Controllers;

/// <summary>
/// API controller for JellyScout plugin.
/// </summary>
[ApiController]
[Route("jellyscout")]
[Authorize(Policy = "DefaultAuthorization")]
public class JellyScoutController : ControllerBase
{
    private readonly ILogger<JellyScoutController> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyScoutController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="libraryManager">The library manager.</param>
    public JellyScoutController(
        ILogger<JellyScoutController> logger,
        ILoggerFactory loggerFactory,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Search for movies and TV shows.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="page">The page number.</param>
    /// <param name="maxResults">The maximum number of results.</param>
    /// <returns>Search results.</returns>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [Required] string query,
        [FromQuery] int page = 1,
        [FromQuery] int maxResults = 20)
    {
        // Comprehensive input validation
        var validationResult = ValidateSearchRequest(query, page, maxResults);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = validationResult.ErrorMessage });
        }

        try
        {
            var sanitizedQuery = SanitizeInput(query);
            var catalogModule = ServiceManager.GetCatalogModule(_libraryManager, _loggerFactory);
            var results = await catalogModule.SearchAsync(sanitizedQuery, page, maxResults);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in search endpoint for query: {Query}", query);
            return StatusCode(500, new { error = "Internal server error occurred during search." });
        }
    }

    /// <summary>
    /// Get detailed information about a movie or TV show.
    /// </summary>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <param name="mediaType">The media type (movie or tv).</param>
    /// <returns>Detailed information.</returns>
    [HttpGet("details/{tmdbId}")]
    public async Task<IActionResult> GetDetails(
        [Required] int tmdbId,
        [FromQuery] string mediaType = "movie")
    {
        // Validate TMDB ID
        if (tmdbId <= 0)
        {
            return BadRequest(new { error = "Invalid TMDB ID. Must be a positive integer." });
        }

        // Validate media type
        var validMediaTypes = new[] { "movie", "tv" };
        if (!validMediaTypes.Contains(mediaType.ToLowerInvariant()))
        {
            return BadRequest(new { error = $"Invalid media type. Must be one of: {string.Join(", ", validMediaTypes)}" });
        }

        try
        {
            var sanitizedMediaType = SanitizeInput(mediaType);
            var tmdbService = ServiceManager.GetTMDBService(_loggerFactory);
            object? details = null;

            if (sanitizedMediaType.Equals("movie", StringComparison.OrdinalIgnoreCase))
            {
                details = await tmdbService.GetMovieDetailsAsync(tmdbId);
            }
            else if (sanitizedMediaType.Equals("tv", StringComparison.OrdinalIgnoreCase))
            {
                details = await tmdbService.GetTVShowDetailsAsync(tmdbId);
            }

            if (details == null)
            {
                return NotFound(new { error = $"No details found for {sanitizedMediaType} with ID {tmdbId}" });
            }

            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting details for {MediaType} ID: {TmdbId}", mediaType, tmdbId);
            return StatusCode(500, new { error = "Internal server error occurred while getting details." });
        }
    }

    /// <summary>
    /// Search for torrents for a specific title.
    /// </summary>
    /// <param name="title">The title to search for.</param>
    /// <param name="year">The release year.</param>
    /// <param name="mediaType">The media type.</param>
    /// <param name="quality">The preferred quality.</param>
    /// <returns>Torrent search results.</returns>
    [HttpGet("torrents")]
    public async Task<IActionResult> SearchTorrents(
        [Required] string title,
        [FromQuery] int? year,
        [FromQuery] string mediaType = "movie",
        [FromQuery] string quality = "1080p")
    {
        // Validate title
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(new { error = "Title is required and cannot be empty." });
        }

        if (title.Length > 500)
        {
            return BadRequest(new { error = "Title is too long. Maximum length is 500 characters." });
        }

        // Validate year
        if (year.HasValue && (year.Value < 1900 || year.Value > DateTime.Now.Year + 10))
        {
            return BadRequest(new { error = $"Invalid year. Must be between 1900 and {DateTime.Now.Year + 10}." });
        }

        // Validate media type
        var validMediaTypes = new[] { "movie", "tv" };
        if (!validMediaTypes.Contains(mediaType.ToLowerInvariant()))
        {
            return BadRequest(new { error = $"Invalid media type. Must be one of: {string.Join(", ", validMediaTypes)}" });
        }

        // Validate quality
        var validQualities = new[] { "4k", "2160p", "1080p", "720p", "480p" };
        if (!validQualities.Contains(quality.ToLowerInvariant()))
        {
            return BadRequest(new { error = $"Invalid quality. Must be one of: {string.Join(", ", validQualities)}" });
        }

        try
        {
            var sanitizedTitle = SanitizeInput(title);
            var sanitizedMediaType = SanitizeInput(mediaType);
            var sanitizedQuality = SanitizeInput(quality);
            
            var streamingService = ServiceManager.GetStreamingService(_loggerFactory);
            var torrents = await streamingService.SearchTorrentsAsync(sanitizedTitle, year, sanitizedMediaType, sanitizedQuality);
            return Ok(new { torrents = torrents.ToArray() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching torrents for: {Title}", title);
            return StatusCode(500, new { error = "Internal server error occurred while searching torrents." });
        }
    }

    /// <summary>
    /// Start streaming a torrent.
    /// </summary>
    /// <param name="request">The streaming request.</param>
    /// <returns>Streaming information.</returns>
    [HttpPost("stream")]
    public async Task<IActionResult> StartStreaming([FromBody] StreamingRequest request)
    {
        // Validate request
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        var validationResult = ValidateStreamingRequest(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = validationResult.ErrorMessage });
        }

        try
        {
            var sanitizedRequest = new StreamingRequest
            {
                MagnetLink = SanitizeInput(request.MagnetLink),
                Title = SanitizeInput(request.Title)
            };

            var streamingService = ServiceManager.GetStreamingService(_loggerFactory);
            var streamingUrl = await streamingService.StartStreamingAsync(sanitizedRequest.MagnetLink, sanitizedRequest.Title);

            if (string.IsNullOrEmpty(streamingUrl))
            {
                return BadRequest(new { error = "Failed to start streaming." });
            }

            return Ok(new { streamingUrl, message = "Streaming started successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting stream for: {Title}", request.Title);
            return StatusCode(500, new { error = "Internal server error occurred while starting stream." });
        }
    }

    /// <summary>
    /// Start downloading a torrent.
    /// </summary>
    /// <param name="request">The download request.</param>
    /// <returns>Download information.</returns>
    [HttpPost("download")]
    public async Task<IActionResult> StartDownload([FromBody] DownloadRequest request)
    {
        // Validate request
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        var validationResult = ValidateDownloadRequest(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = validationResult.ErrorMessage });
        }

        try
        {
            var sanitizedRequest = new DownloadRequest
            {
                MagnetLink = SanitizeInput(request.MagnetLink),
                Title = SanitizeInput(request.Title)
            };

            var streamingService = ServiceManager.GetStreamingService(_loggerFactory);
            var success = await streamingService.StartDownloadAsync(sanitizedRequest.MagnetLink, sanitizedRequest.Title);

            if (!success)
            {
                return BadRequest(new { error = "Failed to start download." });
            }

            return Ok(new { message = "Download started successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting download for: {Title}", request.Title);
            return StatusCode(500, new { error = "Internal server error occurred while starting download." });
        }
    }

    /// <summary>
    /// Get plugin status and configuration.
    /// </summary>
    /// <returns>Plugin status.</returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var catalogModule = ServiceManager.GetCatalogModule(_libraryManager, _loggerFactory);
            var status = catalogModule.GetStatus();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plugin status");
            return StatusCode(500, new { error = "Internal server error occurred while getting status." });
        }
    }

    /// <summary>
    /// Get health status of all external services.
    /// </summary>
    /// <returns>Health status of all services.</returns>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealthStatus()
    {
        try
        {
            var healthCheckService = ServiceManager.GetHealthCheckService(_loggerFactory);
            var healthStatus = await healthCheckService.CheckAllServicesAsync();
            
            // Return appropriate HTTP status based on overall health
            var statusCode = healthStatus.OverallStatus switch
            {
                HealthStatus.Healthy => 200,
                HealthStatus.Degraded => 207, // Multi-Status
                HealthStatus.Unhealthy => 503, // Service Unavailable
                _ => 500
            };

            return StatusCode(statusCode, healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health status");
            return StatusCode(500, new { error = "Internal server error occurred while checking health status." });
        }
    }

    /// <summary>
    /// Get health status of a specific service.
    /// </summary>
    /// <param name="serviceName">The service name (tmdb, streamio, sonarr, radarr).</param>
    /// <returns>Health status of the specified service.</returns>
    [HttpGet("health/{serviceName}")]
    public async Task<IActionResult> GetServiceHealth(string serviceName)
    {
        // Validate service name
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return BadRequest(new { error = "Service name is required." });
        }

        var validServices = new[] { "tmdb", "streamio", "sonarr", "radarr" };
        if (!validServices.Contains(serviceName.ToLowerInvariant()))
        {
            return BadRequest(new { error = $"Invalid service name. Must be one of: {string.Join(", ", validServices)}" });
        }

        try
        {
            var healthCheckService = ServiceManager.GetHealthCheckService(_loggerFactory);
            ServiceHealthStatus healthStatus;

            switch (serviceName.ToLowerInvariant())
            {
                case "tmdb":
                    healthStatus = await healthCheckService.CheckTMDBHealthAsync();
                    break;
                case "streamio":
                    healthStatus = await healthCheckService.CheckStreamioHealthAsync();
                    break;
                case "sonarr":
                    healthStatus = await healthCheckService.CheckSonarrHealthAsync();
                    break;
                case "radarr":
                    healthStatus = await healthCheckService.CheckRadarrHealthAsync();
                    break;
                default:
                    return BadRequest(new { error = "Invalid service name." });
            }

            // Return appropriate HTTP status based on service health
            var statusCode = healthStatus.Status switch
            {
                HealthStatus.Healthy => 200,
                HealthStatus.Degraded => 207, // Multi-Status
                HealthStatus.Unhealthy => 503, // Service Unavailable
                _ => 500
            };

            return StatusCode(statusCode, healthStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health status for service: {ServiceName}", serviceName);
            return StatusCode(500, new { error = "Internal server error occurred while checking service health." });
        }
    }

    // Advanced Filtering and Sorting Endpoints
    [HttpPost("search/advanced")]
    public async Task<IActionResult> AdvancedSearch([FromBody] AdvancedSearchRequest request)
    {
        try
        {
            var validationResult = ValidateAdvancedSearchRequest(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var tmdbService = ServiceManager.GetTMDBService(_loggerFactory);
            var filteringService = ServiceManager.GetFilteringService(_loggerFactory);

            // Get base search results
            var searchResults = await tmdbService.SearchAsync(request.Query, request.Page);
            
            // Apply advanced filters
            var filteredResults = filteringService.ApplyFiltersAndSorting(searchResults, request.Filters);

            return Ok(new
            {
                results = filteredResults,
                filters = request.Filters,
                totalPages = request.Page,
                currentPage = request.Page
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing advanced search");
            return StatusCode(500, new { error = "Advanced search failed" });
        }
    }

    [HttpGet("search/filters")]
    public async Task<IActionResult> GetAvailableFilters([FromQuery] string query = "")
    {
        try
        {
            var tmdbService = ServiceManager.GetTMDBService(_loggerFactory);
            var filteringService = ServiceManager.GetFilteringService(_loggerFactory);

            // Get sample results to determine available filters
            var sampleResults = await tmdbService.SearchAsync(query, 1);
            var availableFilters = filteringService.GetAvailableFilters(sampleResults);

            return Ok(availableFilters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available filters");
            return StatusCode(500, new { error = "Failed to get filters" });
        }
    }

    // Playlist Management Endpoints
    [HttpGet("playlists")]
    public async Task<IActionResult> GetPlaylists([FromQuery] string userId)
    {
        try
        {
            var validationResult = ValidateUserId(userId);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var playlistService = ServiceManager.GetPlaylistService(_loggerFactory);
            var playlists = await playlistService.GetUserPlaylistsAsync(userId);

            return Ok(playlists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user playlists");
            return StatusCode(500, new { error = "Failed to get playlists" });
        }
    }

    [HttpPost("playlists")]
    public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistRequest request)
    {
        try
        {
            var validationResult = ValidateCreatePlaylistRequest(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var playlistService = ServiceManager.GetPlaylistService(_loggerFactory);
            var playlist = await playlistService.CreatePlaylistAsync(
                request.UserId, 
                request.Name, 
                request.Description, 
                request.IsPublic);

            return Ok(playlist);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist");
            return StatusCode(500, new { error = "Failed to create playlist" });
        }
    }

    [HttpGet("playlists/{playlistId}")]
    public async Task<IActionResult> GetPlaylist(string playlistId, [FromQuery] string userId)
    {
        try
        {
            var validationResult = ValidateUserId(userId);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var playlistService = ServiceManager.GetPlaylistService(_loggerFactory);
            var playlist = await playlistService.GetPlaylistAsync(playlistId, userId);

            if (playlist == null)
            {
                return NotFound(new { error = "Playlist not found" });
            }

            return Ok(playlist);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playlist");
            return StatusCode(500, new { error = "Failed to get playlist" });
        }
    }

    [HttpPost("playlists/{playlistId}/items")]
    public async Task<IActionResult> AddToPlaylist(string playlistId, [FromBody] AddToPlaylistRequest request)
    {
        try
        {
            var validationResult = ValidateAddToPlaylistRequest(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var playlistService = ServiceManager.GetPlaylistService(_loggerFactory);
            var success = await playlistService.AddToPlaylistAsync(
                playlistId, 
                request.UserId, 
                request.TmdbId, 
                request.MediaType, 
                request.Title, 
                request.PosterPath);

            if (!success)
            {
                return BadRequest(new { error = "Failed to add item to playlist" });
            }

            return Ok(new { message = "Item added to playlist successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to playlist");
            return StatusCode(500, new { error = "Failed to add item to playlist" });
        }
    }

    [HttpDelete("playlists/{playlistId}/items/{itemId}")]
    public async Task<IActionResult> RemoveFromPlaylist(string playlistId, string itemId, [FromQuery] string userId)
    {
        try
        {
            var validationResult = ValidateUserId(userId);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var playlistService = ServiceManager.GetPlaylistService(_loggerFactory);
            var success = await playlistService.RemoveFromPlaylistAsync(playlistId, userId, itemId);

            if (!success)
            {
                return BadRequest(new { error = "Failed to remove item from playlist" });
            }

            return Ok(new { message = "Item removed from playlist successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from playlist");
            return StatusCode(500, new { error = "Failed to remove item from playlist" });
        }
    }

    [HttpDelete("playlists/{playlistId}")]
    public async Task<IActionResult> DeletePlaylist(string playlistId, [FromQuery] string userId)
    {
        try
        {
            var validationResult = ValidateUserId(userId);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var playlistService = ServiceManager.GetPlaylistService(_loggerFactory);
            var success = await playlistService.DeletePlaylistAsync(playlistId, userId);

            if (!success)
            {
                return BadRequest(new { error = "Failed to delete playlist" });
            }

            return Ok(new { message = "Playlist deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting playlist");
            return StatusCode(500, new { error = "Failed to delete playlist" });
        }
    }

    // Queue Management Endpoints
    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue([FromQuery] string userId)
    {
        try
        {
            var validationResult = ValidateUserId(userId);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var playlistService = ServiceManager.GetPlaylistService(_loggerFactory);
            var queue = await playlistService.GetQueueAsync(userId);

            return Ok(queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user queue");
            return StatusCode(500, new { error = "Failed to get queue" });
        }
    }

    [HttpPost("queue/play")]
    public async Task<IActionResult> PlayPlaylist([FromBody] PlayPlaylistRequest request)
    {
        try
        {
            var validationResult = ValidatePlayPlaylistRequest(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var playlistService = ServiceManager.GetPlaylistService(_loggerFactory);
            var success = await playlistService.PlayPlaylistAsync(request.PlaylistId, request.UserId, request.Shuffle);

            if (!success)
            {
                return BadRequest(new { error = "Failed to play playlist" });
            }

            return Ok(new { message = "Playlist added to queue successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing playlist");
            return StatusCode(500, new { error = "Failed to play playlist" });
        }
    }

    [HttpDelete("queue")]
    public async Task<IActionResult> ClearQueue([FromQuery] string userId)
    {
        try
        {
            var validationResult = ValidateUserId(userId);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            var playlistService = ServiceManager.GetPlaylistService(_loggerFactory);
            var success = await playlistService.ClearQueueAsync(userId);

            if (!success)
            {
                return BadRequest(new { error = "Failed to clear queue" });
            }

            return Ok(new { message = "Queue cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing queue");
            return StatusCode(500, new { error = "Failed to clear queue" });
        }
    }

    // Configuration Validation Endpoints
    [HttpPost("config/validate")]
    public async Task<IActionResult> ValidateConfiguration([FromBody] PluginConfiguration config)
    {
        try
        {
            var validationService = ServiceManager.GetConfigurationValidationService(_loggerFactory);
            var result = await validationService.ValidateConfigurationAsync(config);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration");
            return StatusCode(500, new { error = "Configuration validation failed" });
        }
    }

    /// <summary>
    /// Saves plugin configuration.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    /// <returns>Success or error response.</returns>
    [HttpPost("config/save")]
    public async Task<IActionResult> SaveConfiguration([FromBody] PluginConfiguration config)
    {
        try
        {
            if (config == null)
            {
                return BadRequest(new { error = "Configuration is required" });
            }

            // Validate configuration first
            var validationService = ServiceManager.GetConfigurationValidationService(_loggerFactory);
            var validationResult = await validationService.ValidateConfigurationAsync(config);

            if (!validationResult.IsValid)
            {
                return BadRequest(new { 
                    error = "Configuration validation failed", 
                    validationErrors = validationResult.Errors.Select(e => e.Message) 
                });
            }

            // Save configuration
            if (Plugin.Instance?.Configuration != null)
            {
                Plugin.Instance.Configuration.TmdbApiKey = config.TmdbApiKey;
                Plugin.Instance.Configuration.EnableStreaming = config.EnableStreaming;
                Plugin.Instance.Configuration.EnableDownloads = config.EnableDownloads;
                Plugin.Instance.Configuration.EnableNotifications = config.EnableNotifications;
                Plugin.Instance.Configuration.AutoCheckLibrary = config.AutoCheckLibrary;
                Plugin.Instance.Configuration.DefaultQuality = config.DefaultQuality;
                Plugin.Instance.Configuration.MaxSearchResults = config.MaxSearchResults;
                Plugin.Instance.Configuration.ApiTimeoutSeconds = config.ApiTimeoutSeconds;
                Plugin.Instance.Configuration.CacheExpirationMinutes = config.CacheExpirationMinutes;
                Plugin.Instance.Configuration.MaxConcurrentRequests = config.MaxConcurrentRequests;
                Plugin.Instance.Configuration.EnableRateLimiting = config.EnableRateLimiting;
                Plugin.Instance.Configuration.RequestsPerSecond = config.RequestsPerSecond;

                // Copy service configurations
                if (config.StreamioConfig != null)
                {
                    Plugin.Instance.Configuration.StreamioConfig = config.StreamioConfig;
                }
                if (config.SonarrConfig != null)
                {
                    Plugin.Instance.Configuration.SonarrConfig = config.SonarrConfig;
                }
                if (config.RadarrConfig != null)
                {
                    Plugin.Instance.Configuration.RadarrConfig = config.RadarrConfig;
                }

                Plugin.Instance.SaveConfiguration();
                
                _logger.LogInformation("Plugin configuration saved successfully");
                return Ok(new { message = "Configuration saved successfully" });
            }

            return StatusCode(500, new { error = "Plugin instance not available" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            return StatusCode(500, new { error = "Failed to save configuration" });
        }
    }

    [HttpGet("config/recommendations")]
    public IActionResult GetConfigurationRecommendations()
    {
        try
        {
            var validationService = ServiceManager.GetConfigurationValidationService(_loggerFactory);
            var config = Plugin.Instance?.Configuration;
            
            if (config == null)
            {
                return BadRequest(new { error = "Plugin configuration not found" });
            }

            var recommendations = validationService.GetRecommendations(config);
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration recommendations");
            return StatusCode(500, new { error = "Failed to get recommendations" });
        }
    }

    /// <summary>
    /// Add a TV show to Sonarr.
    /// </summary>
    /// <param name="request">The add TV show request.</param>
    /// <returns>Success status.</returns>
    [HttpPost("sonarr/add")]
    public async Task<IActionResult> AddToSonarr([FromBody] AddMediaRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        var validationResult = ValidateAddMediaRequest(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = validationResult.ErrorMessage });
        }

        try
        {
            var sonarrService = ServiceManager.GetSonarrService(_loggerFactory);
            var success = await sonarrService.AddTVShowAsync(request.Title, request.TmdbId, request.Year, request.QualityProfileId);

            if (success)
            {
                return Ok(new { message = $"Successfully added '{request.Title}' to Sonarr" });
            }
            else
            {
                return BadRequest(new { error = "Failed to add TV show to Sonarr" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding TV show to Sonarr: {Title}", request.Title);
            return StatusCode(500, new { error = "Internal server error occurred while adding to Sonarr." });
        }
    }

    /// <summary>
    /// Add a movie to Radarr.
    /// </summary>
    /// <param name="request">The add movie request.</param>
    /// <returns>Success status.</returns>
    [HttpPost("radarr/add")]
    public async Task<IActionResult> AddToRadarr([FromBody] AddMediaRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        var validationResult = ValidateAddMediaRequest(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = validationResult.ErrorMessage });
        }

        try
        {
            var radarrService = ServiceManager.GetRadarrService(_loggerFactory);
            var success = await radarrService.AddMovieAsync(request.Title, request.TmdbId, request.Year, request.QualityProfileId);

            if (success)
            {
                return Ok(new { message = $"Successfully added '{request.Title}' to Radarr" });
            }
            else
            {
                return BadRequest(new { error = "Failed to add movie to Radarr" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding movie to Radarr: {Title}", request.Title);
            return StatusCode(500, new { error = "Internal server error occurred while adding to Radarr." });
        }
    }

    /// <summary>
    /// Gets the download status of a TV show from Sonarr.
    /// </summary>
    /// <param name="tmdbId">The TMDB ID of the TV show.</param>
    /// <returns>The download status.</returns>
    [HttpGet("sonarr/status/{tmdbId}")]
    public async Task<IActionResult> GetSonarrStatus(int tmdbId)
    {
        try
        {
            if (tmdbId <= 0)
            {
                return BadRequest(new { error = "Invalid TMDB ID" });
            }

            var sonarrService = ServiceManager.GetSonarrService(_loggerFactory);
            var status = await sonarrService.GetDownloadStatusAsync(tmdbId);
            
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Sonarr status for TMDB ID: {TmdbId}", tmdbId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets the download status of a movie from Radarr.
    /// </summary>
    /// <param name="tmdbId">The TMDB ID of the movie.</param>
    /// <returns>The download status.</returns>
    [HttpGet("radarr/status/{tmdbId}")]
    public async Task<IActionResult> GetRadarrStatus(int tmdbId)
    {
        try
        {
            if (tmdbId <= 0)
            {
                return BadRequest(new { error = "Invalid TMDB ID" });
            }

            var radarrService = ServiceManager.GetRadarrService(_loggerFactory);
            var status = await radarrService.GetDownloadStatusAsync(tmdbId);
            
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Radarr status for TMDB ID: {TmdbId}", tmdbId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets the combined download status for all active downloads.
    /// </summary>
    /// <returns>The combined download queue status.</returns>
    [HttpGet("downloads/queue")]
    public async Task<IActionResult> GetDownloadQueue()
    {
        try
        {
            var sonarrService = ServiceManager.GetSonarrService(_loggerFactory);
            var radarrService = ServiceManager.GetRadarrService(_loggerFactory);
            
            var result = new
            {
                sonarr = new List<object>(),
                radarr = new List<object>(),
                lastUpdated = DateTime.UtcNow
            };

            // Note: This would need public queue methods in the services
            // For now, returning empty arrays as the queue methods are private
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting download queue");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets trending movies and TV shows.
    /// </summary>
    /// <param name="mediaType">The media type (all, movie, tv).</param>
    /// <param name="timeWindow">The time window (day, week).</param>
    /// <param name="language">The language (default: en-US).</param>
    /// <returns>Trending content.</returns>
    [HttpGet("trending/{mediaType}/{timeWindow}")]
    public async Task<IActionResult> GetTrending(
        [Required] string mediaType,
        [Required] string timeWindow,
        [FromQuery] string language = "en-US")
    {
        // Validate media type
        var validMediaTypes = new[] { "all", "movie", "tv" };
        if (!validMediaTypes.Contains(mediaType.ToLowerInvariant()))
        {
            return BadRequest(new { error = $"Invalid media type. Must be one of: {string.Join(", ", validMediaTypes)}" });
        }

        // Validate time window
        var validTimeWindows = new[] { "day", "week" };
        if (!validTimeWindows.Contains(timeWindow.ToLowerInvariant()))
        {
            return BadRequest(new { error = $"Invalid time window. Must be one of: {string.Join(", ", validTimeWindows)}" });
        }

        try
        {
            var tmdbService = ServiceManager.GetTMDBService(_loggerFactory);
            var trendingContent = await tmdbService.GetTrendingAsync(mediaType, timeWindow, language);
            
            return Ok(new { 
                results = trendingContent.ToArray(),
                mediaType,
                timeWindow,
                language
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trending content for {MediaType} in {TimeWindow}", mediaType, timeWindow);
            return StatusCode(500, new { error = "Internal server error occurred while getting trending content." });
        }
    }

    /// <summary>
    /// Gets popular movies.
    /// </summary>
    /// <param name="page">The page number (default: 1).</param>
    /// <param name="language">The language (default: en-US).</param>
    /// <param name="region">The region (default: US).</param>
    /// <returns>Popular movies.</returns>
    [HttpGet("popular/movies")]
    public async Task<IActionResult> GetPopularMovies(
        [FromQuery] int page = 1,
        [FromQuery] string language = "en-US",
        [FromQuery] string region = "US")
    {
        // Validate page
        if (page < 1 || page > 1000)
        {
            return BadRequest(new { error = "Page must be between 1 and 1000." });
        }

        try
        {
            var tmdbService = ServiceManager.GetTMDBService(_loggerFactory);
            var popularMovies = await tmdbService.GetPopularMoviesAsync(page, language, region);
            
            return Ok(new { 
                results = popularMovies.ToArray(),
                page,
                language,
                region
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular movies");
            return StatusCode(500, new { error = "Internal server error occurred while getting popular movies." });
        }
    }

    /// <summary>
    /// Gets popular TV shows.
    /// </summary>
    /// <param name="page">The page number (default: 1).</param>
    /// <param name="language">The language (default: en-US).</param>
    /// <returns>Popular TV shows.</returns>
    [HttpGet("popular/tv")]
    public async Task<IActionResult> GetPopularTVShows(
        [FromQuery] int page = 1,
        [FromQuery] string language = "en-US")
    {
        // Validate page
        if (page < 1 || page > 1000)
        {
            return BadRequest(new { error = "Page must be between 1 and 1000." });
        }

        try
        {
            var tmdbService = ServiceManager.GetTMDBService(_loggerFactory);
            var popularTVShows = await tmdbService.GetPopularTVShowsAsync(page, language);
            
            return Ok(new { 
                results = popularTVShows.ToArray(),
                page,
                language
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular TV shows");
            return StatusCode(500, new { error = "Internal server error occurred while getting popular TV shows." });
        }
    }

    #region Validation Methods

    /// <summary>
    /// Validates a search request.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="page">The page number.</param>
    /// <param name="maxResults">The maximum number of results.</param>
    /// <returns>Validation result.</returns>
    private ValidationResult ValidateSearchRequest(string query, int page, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ValidationResult(false, "Search query is required and cannot be empty.");
        }

        if (query.Length > 500)
        {
            return new ValidationResult(false, "Search query is too long. Maximum length is 500 characters.");
        }

        if (page < 1 || page > 1000)
        {
            return new ValidationResult(false, "Page number must be between 1 and 1000.");
        }

        if (maxResults < 1 || maxResults > 100)
        {
            return new ValidationResult(false, "Maximum results must be between 1 and 100.");
        }

        // Check for potentially malicious patterns
        if (ContainsMaliciousPatterns(query))
        {
            return new ValidationResult(false, "Search query contains invalid characters.");
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// Validates a streaming request.
    /// </summary>
    /// <param name="request">The streaming request.</param>
    /// <returns>Validation result.</returns>
    private ValidationResult ValidateStreamingRequest(StreamingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MagnetLink))
        {
            return new ValidationResult(false, "Magnet link is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new ValidationResult(false, "Title is required.");
        }

        if (request.Title.Length > 500)
        {
            return new ValidationResult(false, "Title is too long. Maximum length is 500 characters.");
        }

        // Validate magnet link format
        if (!IsValidMagnetLink(request.MagnetLink))
        {
            return new ValidationResult(false, "Invalid magnet link format.");
        }

        // Check for potentially malicious patterns
        if (ContainsMaliciousPatterns(request.Title) || ContainsMaliciousPatterns(request.MagnetLink))
        {
            return new ValidationResult(false, "Request contains invalid characters.");
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// Validates a download request.
    /// </summary>
    /// <param name="request">The download request.</param>
    /// <returns>Validation result.</returns>
    private ValidationResult ValidateDownloadRequest(DownloadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MagnetLink))
        {
            return new ValidationResult(false, "Magnet link is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new ValidationResult(false, "Title is required.");
        }

        if (request.Title.Length > 500)
        {
            return new ValidationResult(false, "Title is too long. Maximum length is 500 characters.");
        }

        // Validate magnet link format
        if (!IsValidMagnetLink(request.MagnetLink))
        {
            return new ValidationResult(false, "Invalid magnet link format.");
        }

        // Check for potentially malicious patterns
        if (ContainsMaliciousPatterns(request.Title) || ContainsMaliciousPatterns(request.MagnetLink))
        {
            return new ValidationResult(false, "Request contains invalid characters.");
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// Sanitizes input by removing potentially harmful characters.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>Sanitized string.</returns>
    private string SanitizeInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Remove HTML tags
        input = Regex.Replace(input, @"<[^>]*>", string.Empty);

        // Remove script injection attempts
        input = Regex.Replace(input, @"<script[^>]*>.*?</script>", string.Empty, RegexOptions.IgnoreCase);

        // Remove SQL injection attempts
        input = Regex.Replace(input, @"('|(\')|(\-\-)|(\;))", string.Empty);

        // Trim whitespace
        input = input.Trim();

        return input;
    }

    /// <summary>
    /// Checks if the input contains potentially malicious patterns.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>True if malicious patterns are found.</returns>
    private bool ContainsMaliciousPatterns(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Check for script injection
        if (Regex.IsMatch(input, @"<script[^>]*>", RegexOptions.IgnoreCase))
        {
            return true;
        }

        // Check for SQL injection patterns
        var sqlPatterns = new[]
        {
            @"(\bunion\b.*\bselect\b)",
            @"(\bselect\b.*\bfrom\b)",
            @"(\binsert\b.*\binto\b)",
            @"(\bupdate\b.*\bset\b)",
            @"(\bdelete\b.*\bfrom\b)",
            @"(\bdrop\b.*\btable\b)",
            @"(\bexec\b.*\bxp_cmdshell\b)",
        };

        foreach (var pattern in sqlPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        // Check for XSS patterns
        var xssPatterns = new[]
        {
            @"javascript:",
            @"vbscript:",
            @"onload=",
            @"onerror=",
            @"onclick=",
            @"onmouseover=",
        };

        foreach (var pattern in xssPatterns)
        {
            if (input.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates if a string is a valid magnet link.
    /// </summary>
    /// <param name="magnetLink">The magnet link.</param>
    /// <returns>True if valid.</returns>
    private bool IsValidMagnetLink(string magnetLink)
    {
        if (string.IsNullOrWhiteSpace(magnetLink))
        {
            return false;
        }

        // Basic magnet link validation
        if (!magnetLink.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check for required xt parameter
        if (!magnetLink.Contains("xt=urn:btih:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check reasonable length
        if (magnetLink.Length > 2000)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a user ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Validation result.</returns>
    private ValidationResult ValidateUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ValidationResult(false, "User ID is required.");
        }

        if (userId.Length > 100)
        {
            return new ValidationResult(false, "User ID is too long.");
        }

        if (ContainsMaliciousPatterns(userId))
        {
            return new ValidationResult(false, "User ID contains invalid characters.");
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// Validates an advanced search request.
    /// </summary>
    /// <param name="request">The search request.</param>
    /// <returns>Validation result.</returns>
    private ValidationResult ValidateAdvancedSearchRequest(AdvancedSearchRequest request)
    {
        if (request == null)
        {
            return new ValidationResult(false, "Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new ValidationResult(false, "Search query is required.");
        }

        if (request.Query.Length > 500)
        {
            return new ValidationResult(false, "Search query is too long.");
        }

        if (request.Page < 1 || request.Page > 1000)
        {
            return new ValidationResult(false, "Page number must be between 1 and 1000.");
        }

        if (ContainsMaliciousPatterns(request.Query))
        {
            return new ValidationResult(false, "Search query contains invalid characters.");
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// Validates a create playlist request.
    /// </summary>
    /// <param name="request">The create playlist request.</param>
    /// <returns>Validation result.</returns>
    private ValidationResult ValidateCreatePlaylistRequest(CreatePlaylistRequest request)
    {
        if (request == null)
        {
            return new ValidationResult(false, "Request is required.");
        }

        var userIdValidation = ValidateUserId(request.UserId);
        if (!userIdValidation.IsValid)
        {
            return userIdValidation;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new ValidationResult(false, "Playlist name is required.");
        }

        if (request.Name.Length > 200)
        {
            return new ValidationResult(false, "Playlist name is too long.");
        }

        if (request.Description != null && request.Description.Length > 1000)
        {
            return new ValidationResult(false, "Playlist description is too long.");
        }

        if (ContainsMaliciousPatterns(request.Name))
        {
            return new ValidationResult(false, "Playlist name contains invalid characters.");
        }

        if (request.Description != null && ContainsMaliciousPatterns(request.Description))
        {
            return new ValidationResult(false, "Playlist description contains invalid characters.");
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// Validates an add to playlist request.
    /// </summary>
    /// <param name="request">The add to playlist request.</param>
    /// <returns>Validation result.</returns>
    private ValidationResult ValidateAddToPlaylistRequest(AddToPlaylistRequest request)
    {
        if (request == null)
        {
            return new ValidationResult(false, "Request is required.");
        }

        var userIdValidation = ValidateUserId(request.UserId);
        if (!userIdValidation.IsValid)
        {
            return userIdValidation;
        }

        if (request.TmdbId <= 0)
        {
            return new ValidationResult(false, "TMDB ID must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(request.MediaType))
        {
            return new ValidationResult(false, "Media type is required.");
        }

        if (!new[] { "movie", "tv" }.Contains(request.MediaType.ToLowerInvariant()))
        {
            return new ValidationResult(false, "Media type must be 'movie' or 'tv'.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new ValidationResult(false, "Title is required.");
        }

        if (request.Title.Length > 500)
        {
            return new ValidationResult(false, "Title is too long.");
        }

        if (ContainsMaliciousPatterns(request.Title))
        {
            return new ValidationResult(false, "Title contains invalid characters.");
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// Validates a play playlist request.
    /// </summary>
    /// <param name="request">The play playlist request.</param>
    /// <returns>Validation result.</returns>
    private ValidationResult ValidatePlayPlaylistRequest(PlayPlaylistRequest request)
    {
        if (request == null)
        {
            return new ValidationResult(false, "Request is required.");
        }

        var userIdValidation = ValidateUserId(request.UserId);
        if (!userIdValidation.IsValid)
        {
            return userIdValidation;
        }

        if (string.IsNullOrWhiteSpace(request.PlaylistId))
        {
            return new ValidationResult(false, "Playlist ID is required.");
        }

        if (ContainsMaliciousPatterns(request.PlaylistId))
        {
            return new ValidationResult(false, "Playlist ID contains invalid characters.");
        }

        return new ValidationResult(true);
    }

    /// <summary>
    /// Validates an add media request.
    /// </summary>
    /// <param name="request">The add media request to validate.</param>
    /// <returns>Validation result.</returns>
    private static ValidationResult ValidateAddMediaRequest(AddMediaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new ValidationResult(false, "Title is required.");
        }

        if (request.Title.Length > 500)
        {
            return new ValidationResult(false, "Title is too long. Maximum length is 500 characters.");
        }

        if (request.TmdbId <= 0)
        {
            return new ValidationResult(false, "Valid TMDB ID is required.");
        }

        if (request.Year.HasValue && (request.Year.Value < 1900 || request.Year.Value > DateTime.Now.Year + 10))
        {
            return new ValidationResult(false, $"Invalid year. Must be between 1900 and {DateTime.Now.Year + 10}.");
        }

        if (request.QualityProfileId.HasValue && request.QualityProfileId.Value <= 0)
        {
            return new ValidationResult(false, "Quality profile ID must be a positive integer.");
        }

        return new ValidationResult(true);
    }

    #endregion
}

/// <summary>
/// Advanced search request model.
/// </summary>
public class AdvancedSearchRequest
{
    /// <summary>
    /// Gets or sets the search query.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the search filters.
    /// </summary>
    public SearchFilters Filters { get; set; } = new();
}

/// <summary>
/// Create playlist request model.
/// </summary>
public class CreatePlaylistRequest
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the playlist name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the playlist description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether the playlist is public.
    /// </summary>
    public bool IsPublic { get; set; }
}

/// <summary>
/// Add to playlist request model.
/// </summary>
public class AddToPlaylistRequest
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TMDB ID.
    /// </summary>
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the poster path.
    /// </summary>
    public string? PosterPath { get; set; }
}

/// <summary>
/// Add media request model for Sonarr/Radarr integration.
/// </summary>
public class AddMediaRequest
{
    /// <summary>
    /// Gets or sets the media title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TMDB ID.
    /// </summary>
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the release year.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the quality profile ID.
    /// </summary>
    public int? QualityProfileId { get; set; }
}

/// <summary>
/// Streaming request model.
/// </summary>
public class StreamingRequest
{
    /// <summary>
    /// Gets or sets the magnet link.
    /// </summary>
    public string MagnetLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media title.
    /// </summary>
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// Download request model.
/// </summary>
public class DownloadRequest
{
    /// <summary>
    /// Gets or sets the magnet link.
    /// </summary>
    public string MagnetLink { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media title.
    /// </summary>
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// Play playlist request model.
/// </summary>
public class PlayPlaylistRequest
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the playlist ID.
    /// </summary>
    public string PlaylistId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to shuffle the playlist.
    /// </summary>
    public bool Shuffle { get; set; } = false;
}

/// <summary>
/// Validation result model.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    /// <param name="isValid">Whether the validation passed.</param>
    /// <param name="errorMessage">The error message if validation failed.</param>
    public ValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    /// <summary>
    /// Gets a value indicating whether the validation passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the error message if validation failed.
    /// </summary>
    public string ErrorMessage { get; }
} 