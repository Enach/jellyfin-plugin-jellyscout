using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyScout.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for monitoring the health of external services.
/// </summary>
public class HealthCheckService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly CacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cacheService">The cache service.</param>
    public HealthCheckService(HttpClient httpClient, ILogger<HealthCheckService> logger, CacheService cacheService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
        
        // Configure HttpClient for health checks
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Performs a comprehensive health check of all external services.
    /// </summary>
    /// <returns>Health check results.</returns>
    public async Task<OverallHealthStatus> CheckAllServicesAsync()
    {
        _logger.LogInformation("Starting comprehensive health check of all external services");

        var tasks = new List<Task<ServiceHealthStatus>>
        {
            CheckTMDBHealthAsync(),
            CheckSonarrHealthAsync(),
            CheckRadarrHealthAsync()
        };

        var results = await Task.WhenAll(tasks);

        var overallStatus = new OverallHealthStatus
        {
            CheckTime = DateTime.UtcNow,
            Services = new List<ServiceHealthStatus>(results)
        };

        // Determine overall health
        var allHealthy = true;
        var anyPartiallyHealthy = false;

        foreach (var service in overallStatus.Services)
        {
            if (service.Status == HealthStatus.Unhealthy)
            {
                allHealthy = false;
            }
            else if (service.Status == HealthStatus.Degraded)
            {
                allHealthy = false;
                anyPartiallyHealthy = true;
            }
        }

        overallStatus.OverallStatus = allHealthy ? HealthStatus.Healthy : 
                                    anyPartiallyHealthy ? HealthStatus.Degraded : 
                                    HealthStatus.Unhealthy;

        _logger.LogInformation("Health check completed. Overall status: {Status}", overallStatus.OverallStatus);

        return overallStatus;
    }

    /// <summary>
    /// Checks the health of TMDB service.
    /// </summary>
    /// <returns>TMDB health status.</returns>
    public async Task<ServiceHealthStatus> CheckTMDBHealthAsync()
    {
        var result = new ServiceHealthStatus
        {
            ServiceName = "TMDB",
            CheckTime = DateTime.UtcNow
        };

        try
        {
            // Use cache to avoid excessive API calls
            var cachedResult = await _cacheService.GetOrCreateAsync("health_check_tmdb", async () =>
            {
                var apiKey = GetTMDBApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return new ServiceHealthStatus
                    {
                        ServiceName = "TMDB",
                        Status = HealthStatus.Unhealthy,
                        Message = "TMDB API key is not configured",
                        CheckTime = DateTime.UtcNow
                    };
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.GetAsync($"https://api.themoviedb.org/3/configuration?api_key={apiKey}");
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    return new ServiceHealthStatus
                    {
                        ServiceName = "TMDB",
                        Status = stopwatch.ElapsedMilliseconds > 5000 ? HealthStatus.Degraded : HealthStatus.Healthy,
                        Message = $"TMDB API is responding. Response time: {stopwatch.ElapsedMilliseconds}ms",
                        ResponseTime = stopwatch.ElapsedMilliseconds,
                        CheckTime = DateTime.UtcNow
                    };
                }
                else
                {
                    return new ServiceHealthStatus
                    {
                        ServiceName = "TMDB",
                        Status = HealthStatus.Unhealthy,
                        Message = $"TMDB API returned {response.StatusCode}: {response.ReasonPhrase}",
                        CheckTime = DateTime.UtcNow
                    };
                }
            }, TimeSpan.FromMinutes(2)); // Cache health checks for 2 minutes

            return cachedResult ?? result;
        }
        catch (TaskCanceledException)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = "TMDB API request timed out";
            _logger.LogWarning("TMDB health check timed out");
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"TMDB health check failed: {ex.Message}";
            _logger.LogError(ex, "TMDB health check failed");
        }

        return result;
    }



    /// <summary>
    /// Checks the health of Sonarr service.
    /// </summary>
    /// <returns>Sonarr health status.</returns>
    public async Task<ServiceHealthStatus> CheckSonarrHealthAsync()
    {
        var result = new ServiceHealthStatus
        {
            ServiceName = "Sonarr",
            CheckTime = DateTime.UtcNow
        };

        try
        {
            var config = GetSonarrConfig();
            if (config == null || !config.Enabled || string.IsNullOrWhiteSpace(config.ServerUrl))
            {
                result.Status = HealthStatus.Unhealthy;
                result.Message = "Sonarr is not configured or disabled";
                return result;
            }

            var cachedResult = await _cacheService.GetOrCreateAsync("health_check_sonarr", async () =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);
                
                var response = await _httpClient.GetAsync($"{config.ServerUrl.TrimEnd('/')}/api/v3/system/status");
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    return new ServiceHealthStatus
                    {
                        ServiceName = "Sonarr",
                        Status = stopwatch.ElapsedMilliseconds > 3000 ? HealthStatus.Degraded : HealthStatus.Healthy,
                        Message = $"Sonarr is responding. Response time: {stopwatch.ElapsedMilliseconds}ms",
                        ResponseTime = stopwatch.ElapsedMilliseconds,
                        CheckTime = DateTime.UtcNow
                    };
                }
                else
                {
                    return new ServiceHealthStatus
                    {
                        ServiceName = "Sonarr",
                        Status = HealthStatus.Unhealthy,
                        Message = $"Sonarr returned {response.StatusCode}: {response.ReasonPhrase}",
                        CheckTime = DateTime.UtcNow
                    };
                }
            }, TimeSpan.FromMinutes(2));

            return cachedResult ?? result;
        }
        catch (TaskCanceledException)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = "Sonarr request timed out";
            _logger.LogWarning("Sonarr health check timed out");
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"Sonarr health check failed: {ex.Message}";
            _logger.LogError(ex, "Sonarr health check failed");
        }

        return result;
    }

    /// <summary>
    /// Checks the health of Radarr service.
    /// </summary>
    /// <returns>Radarr health status.</returns>
    public async Task<ServiceHealthStatus> CheckRadarrHealthAsync()
    {
        var result = new ServiceHealthStatus
        {
            ServiceName = "Radarr",
            CheckTime = DateTime.UtcNow
        };

        try
        {
            var config = GetRadarrConfig();
            if (config == null || !config.Enabled || string.IsNullOrWhiteSpace(config.ServerUrl))
            {
                result.Status = HealthStatus.Unhealthy;
                result.Message = "Radarr is not configured or disabled";
                return result;
            }

            var cachedResult = await _cacheService.GetOrCreateAsync("health_check_radarr", async () =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);
                
                var response = await _httpClient.GetAsync($"{config.ServerUrl.TrimEnd('/')}/api/v3/system/status");
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    return new ServiceHealthStatus
                    {
                        ServiceName = "Radarr",
                        Status = stopwatch.ElapsedMilliseconds > 3000 ? HealthStatus.Degraded : HealthStatus.Healthy,
                        Message = $"Radarr is responding. Response time: {stopwatch.ElapsedMilliseconds}ms",
                        ResponseTime = stopwatch.ElapsedMilliseconds,
                        CheckTime = DateTime.UtcNow
                    };
                }
                else
                {
                    return new ServiceHealthStatus
                    {
                        ServiceName = "Radarr",
                        Status = HealthStatus.Unhealthy,
                        Message = $"Radarr returned {response.StatusCode}: {response.ReasonPhrase}",
                        CheckTime = DateTime.UtcNow
                    };
                }
            }, TimeSpan.FromMinutes(2));

            return cachedResult ?? result;
        }
        catch (TaskCanceledException)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = "Radarr request timed out";
            _logger.LogWarning("Radarr health check timed out");
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = $"Radarr health check failed: {ex.Message}";
            _logger.LogError(ex, "Radarr health check failed");
        }

        return result;
    }

    private string? GetTMDBApiKey()
    {
        // Try to get from environment variable first
        var apiKey = Environment.GetEnvironmentVariable("TMDB_API_KEY");
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Try to get from plugin configuration
            apiKey = Plugin.Instance?.Configuration?.TmdbApiKey;
        }

        return apiKey;
    }



    private SonarrConfiguration? GetSonarrConfig()
    {
        return Plugin.Instance?.Configuration?.SonarrConfig;
    }

    private RadarrConfiguration? GetRadarrConfig()
    {
        return Plugin.Instance?.Configuration?.RadarrConfig;
    }
}

/// <summary>
/// Represents the health status of a service.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Service is healthy and responding normally.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Service is responding but performance is degraded.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Service is not responding or has failed.
    /// </summary>
    Unhealthy = 2
}

/// <summary>
/// Represents the health status of a single service.
/// </summary>
public class ServiceHealthStatus
{
    /// <summary>
    /// Gets or sets the service name.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the health status.
    /// </summary>
    public HealthStatus Status { get; set; } = HealthStatus.Unhealthy;

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response time in milliseconds.
    /// </summary>
    public long? ResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the check time.
    /// </summary>
    public DateTime CheckTime { get; set; }
}

/// <summary>
/// Represents the overall health status of all services.
/// </summary>
public class OverallHealthStatus
{
    /// <summary>
    /// Gets or sets the overall health status.
    /// </summary>
    public HealthStatus OverallStatus { get; set; } = HealthStatus.Unhealthy;

    /// <summary>
    /// Gets or sets the individual service statuses.
    /// </summary>
    public List<ServiceHealthStatus> Services { get; set; } = new();

    /// <summary>
    /// Gets or sets the check time.
    /// </summary>
    public DateTime CheckTime { get; set; }
} 