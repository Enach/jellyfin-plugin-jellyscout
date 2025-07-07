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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using static Jellyfin.Plugin.JellyScout.Services.HealthCheckService;

namespace Jellyfin.Plugin.JellyScout.Controllers;

/// <summary>
/// JellyScout API Controller - Simplified version focused on core functionality
/// </summary>
[ApiController]
[Route("jellyscout")]
public class JellyScoutController : ControllerBase
{
    private readonly ILogger<JellyScoutController> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public JellyScoutController(
        ILogger<JellyScoutController> logger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Search for movies and TV shows using TMDB
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="page">Page number</param>
    /// <param name="maxResults">Maximum results per page</param>
    /// <returns>Search results</returns>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [Required] string query,
        [FromQuery] int page = 1,
        [FromQuery] int maxResults = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { error = "Search query is required" });
            }

            if (page < 1) page = 1;
            if (maxResults < 1) maxResults = 1;
            if (maxResults > 100) maxResults = 100;

            var tmdbService = ServiceManager.GetTMDBService(_loggerFactory);
            var results = await tmdbService.SearchAsync(query, page);

            return Ok(new
            {
                query = query,
                page = page,
                maxResults = maxResults,
                results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for content: {Query}", query);
            return StatusCode(500, new { error = "Search failed" });
        }
    }

    /// <summary>
    /// Get details for a specific movie or TV show
    /// </summary>
    /// <param name="tmdbId">TMDB ID</param>
    /// <param name="mediaType">Media type (movie or tv)</param>
    /// <returns>Media details</returns>
    [HttpGet("details/{tmdbId}")]
    public async Task<IActionResult> GetDetails(
        [Required] int tmdbId,
        [FromQuery] string mediaType = "movie")
    {
        try
        {
            if (tmdbId <= 0)
            {
                return BadRequest(new { error = "Valid TMDB ID is required" });
            }

            var tmdbService = ServiceManager.GetTMDBService(_loggerFactory);
            object details;

            if (mediaType.ToLower() == "tv")
            {
                details = await tmdbService.GetTVShowDetailsAsync(tmdbId);
            }
            else
            {
                details = await tmdbService.GetMovieDetailsAsync(tmdbId);
            }

            if (details == null)
            {
                return NotFound(new { error = "Content not found" });
            }

            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting details for TMDB ID: {TmdbId}", tmdbId);
            return StatusCode(500, new { error = "Failed to get details" });
        }
    }

    /// <summary>
    /// Add a TV show to Sonarr
    /// </summary>
    /// <param name="request">The add TV show request</param>
    /// <returns>Success status</returns>
    [HttpPost("sonarr/add")]
    public async Task<IActionResult> AddToSonarr([FromBody] AddMediaRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Title) || request.TmdbId <= 0)
            {
                return BadRequest(new { error = "Valid title and TMDB ID are required" });
            }

            var config = Plugin.Instance?.Configuration;
            if (config?.SonarrConfig?.Enabled != true)
            {
                return BadRequest(new { error = "Sonarr integration is not enabled" });
            }

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
            _logger.LogError(ex, "Error adding TV show to Sonarr: {Title}", request?.Title);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Add a movie to Radarr
    /// </summary>
    /// <param name="request">The add movie request</param>
    /// <returns>Success status</returns>
    [HttpPost("radarr/add")]
    public async Task<IActionResult> AddToRadarr([FromBody] AddMediaRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Title) || request.TmdbId <= 0)
            {
                return BadRequest(new { error = "Valid title and TMDB ID are required" });
            }

            var config = Plugin.Instance?.Configuration;
            if (config?.RadarrConfig?.Enabled != true)
            {
                return BadRequest(new { error = "Radarr integration is not enabled" });
            }

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
            _logger.LogError(ex, "Error adding movie to Radarr: {Title}", request?.Title);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get the status of a TV show in Sonarr
    /// </summary>
    /// <param name="tmdbId">TMDB ID of the TV show</param>
    /// <returns>Status information</returns>
    [HttpGet("sonarr/status/{tmdbId}")]
    public async Task<IActionResult> GetSonarrStatus(int tmdbId)
    {
        try
        {
            if (tmdbId <= 0)
            {
                return BadRequest(new { error = "Valid TMDB ID is required" });
            }

            var config = Plugin.Instance?.Configuration;
            if (config?.SonarrConfig?.Enabled != true)
            {
                return BadRequest(new { error = "Sonarr integration is not enabled" });
            }

                            var sonarrService = ServiceManager.GetSonarrService(_loggerFactory);
                var status = await sonarrService.GetDownloadStatusAsync(tmdbId);

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Sonarr status for TMDB ID: {TmdbId}", tmdbId);
            return StatusCode(500, new { error = "Failed to get status" });
        }
    }

    /// <summary>
    /// Get the status of a movie in Radarr
    /// </summary>
    /// <param name="tmdbId">TMDB ID of the movie</param>
    /// <returns>Status information</returns>
    [HttpGet("radarr/status/{tmdbId}")]
    public async Task<IActionResult> GetRadarrStatus(int tmdbId)
    {
        try
        {
            if (tmdbId <= 0)
            {
                return BadRequest(new { error = "Valid TMDB ID is required" });
            }

            var config = Plugin.Instance?.Configuration;
            if (config?.RadarrConfig?.Enabled != true)
            {
                return BadRequest(new { error = "Radarr integration is not enabled" });
            }

                            var radarrService = ServiceManager.GetRadarrService(_loggerFactory);
                var status = await radarrService.GetDownloadStatusAsync(tmdbId);

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Radarr status for TMDB ID: {TmdbId}", tmdbId);
            return StatusCode(500, new { error = "Failed to get status" });
        }
    }

    /// <summary>
    /// Get basic plugin status
    /// </summary>
    /// <returns>Plugin status</returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var config = Plugin.Instance?.Configuration;
            
            return Ok(new
            {
                version = Plugin.Instance?.Version?.ToString() ?? "Unknown",
                tmdbConfigured = !string.IsNullOrWhiteSpace(config?.TmdbApiKey),
                sonarrEnabled = config?.SonarrConfig?.Enabled == true,
                radarrEnabled = config?.RadarrConfig?.Enabled == true,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plugin status");
            return StatusCode(500, new { error = "Failed to get status" });
        }
    }
}

/// <summary>
/// Request model for adding media to external services
/// </summary>
public class AddMediaRequest
{
    public string Title { get; set; } = string.Empty;
    public int TmdbId { get; set; }
    public int? Year { get; set; }
    public int? QualityProfileId { get; set; }
} 