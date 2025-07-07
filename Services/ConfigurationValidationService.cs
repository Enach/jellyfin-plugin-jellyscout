using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.JellyScout.Configuration;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for validating plugin configuration settings.
/// </summary>
public class ConfigurationValidationService
{
    private readonly ILogger<ConfigurationValidationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly CacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValidationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="cacheService">The cache service.</param>
    public ConfigurationValidationService(ILogger<ConfigurationValidationService> logger, HttpClient httpClient, CacheService cacheService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Validates the complete plugin configuration.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>Validation results.</returns>
    public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(PluginConfiguration config)
    {
        var result = new ConfigurationValidationResult();

        if (config == null)
        {
            result.AddError("Configuration", "Configuration is null");
            return result;
        }

        // Validate TMDB configuration
        await ValidateTmdbConfigurationAsync(config, result);

        // Validate Streamio configuration
        await ValidateStreamioConfigurationAsync(config.Streamio, result);

        // Validate Sonarr configuration
        await ValidateSonarrConfigurationAsync(config.Sonarr, result);

        // Validate Radarr configuration
        await ValidateRadarrConfigurationAsync(config.Radarr, result);

        // Validate general settings
        ValidateGeneralSettings(config, result);

        result.IsValid = !result.Errors.Any();
        _logger.LogInformation("Configuration validation completed. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}", 
            result.IsValid, result.Errors.Count, result.Warnings.Count);

        return result;
    }

    /// <summary>
    /// Validates TMDB configuration.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="result">The validation result.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ValidateTmdbConfigurationAsync(PluginConfiguration config, ConfigurationValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            result.AddError("TMDB.ApiKey", "TMDB API key is required");
            return;
        }

        if (config.TmdbApiKey.Length < 20 || config.TmdbApiKey.Length > 50)
        {
            result.AddWarning("TMDB.ApiKey", "TMDB API key format appears invalid (should be 20-50 characters)");
        }

        // Test TMDB API connectivity
        try
        {
            var cacheKey = $"tmdb_validation_{config.TmdbApiKey.GetHashCode()}";
            var cachedResult = _cacheService.Get<object>(cacheKey);
            bool isValid;
            
            if (cachedResult == null)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"https://api.themoviedb.org/3/configuration?api_key={config.TmdbApiKey}");
                
                var response = await _httpClient.SendAsync(request);
                isValid = response.IsSuccessStatusCode;
                
                _cacheService.Set(cacheKey, isValid, TimeSpan.FromMinutes(30));
            }
            else
            {
                isValid = (bool)cachedResult;
            }

            if (isValid)
            {
                result.AddSuccess("TMDB.ApiKey", "TMDB API key is valid and accessible");
            }
            else
            {
                result.AddError("TMDB.ApiKey", "TMDB API key is invalid or inaccessible");
            }
        }
        catch (Exception ex)
        {
            result.AddError("TMDB.ApiKey", $"Failed to validate TMDB API key: {ex.Message}");
            _logger.LogError(ex, "Error validating TMDB configuration");
        }
    }

    /// <summary>
    /// Validates Streamio configuration.
    /// </summary>
    /// <param name="config">The Streamio configuration.</param>
    /// <param name="result">The validation result.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ValidateStreamioConfigurationAsync(StreamioConfiguration config, ConfigurationValidationResult result)
    {
        if (config == null)
        {
            result.AddWarning("Streamio", "Streamio configuration is not set");
            return;
        }

        // Validate server URL
        if (string.IsNullOrWhiteSpace(config.ServerUrl))
        {
            result.AddError("Streamio.ServerUrl", "Streamio server URL is required");
        }
        else if (!IsValidUrl(config.ServerUrl))
        {
            result.AddError("Streamio.ServerUrl", "Streamio server URL is not a valid URL");
        }
        else
        {
            // Test connectivity
            try
            {
                var cacheKey = $"streamio_validation_{config.ServerUrl.GetHashCode()}";
                var cachedResult = _cacheService.Get<object>(cacheKey);
                bool isValid;
                
                if (cachedResult == null)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{config.ServerUrl.TrimEnd('/')}/api/health");
                    if (!string.IsNullOrEmpty(config.ApiKey))
                    {
                        request.Headers.Add("X-API-Key", config.ApiKey);
                    }
                    
                    var response = await _httpClient.SendAsync(request);
                    isValid = response.IsSuccessStatusCode;
                    
                    _cacheService.Set(cacheKey, isValid, TimeSpan.FromMinutes(15));
                }
                else
                {
                    isValid = (bool)cachedResult;
                }

                if (isValid)
                {
                    result.AddSuccess("Streamio.ServerUrl", "Streamio server is accessible");
                }
                else
                {
                    result.AddError("Streamio.ServerUrl", "Streamio server is not accessible");
                }
            }
            catch (Exception ex)
            {
                result.AddError("Streamio.ServerUrl", $"Failed to connect to Streamio server: {ex.Message}");
                _logger.LogError(ex, "Error validating Streamio configuration");
            }
        }

        // Validate API key
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            result.AddWarning("Streamio.ApiKey", "Streamio API key is recommended for authentication");
        }

        // Validate timeout
        if (config.TimeoutSeconds <= 0)
        {
            result.AddError("Streamio.TimeoutSeconds", "Streamio timeout must be greater than 0");
        }
        else if (config.TimeoutSeconds > 300)
        {
            result.AddWarning("Streamio.TimeoutSeconds", "Streamio timeout is very high (>5 minutes)");
        }
    }

    /// <summary>
    /// Validates Sonarr configuration.
    /// </summary>
    /// <param name="config">The Sonarr configuration.</param>
    /// <param name="result">The validation result.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ValidateSonarrConfigurationAsync(SonarrConfiguration config, ConfigurationValidationResult result)
    {
        if (config == null)
        {
            result.AddWarning("Sonarr", "Sonarr configuration is not set - TV show search will be limited");
            return;
        }

        // Validate server URL
        if (string.IsNullOrWhiteSpace(config.ServerUrl))
        {
            result.AddError("Sonarr.ServerUrl", "Sonarr server URL is required");
        }
        else if (!IsValidUrl(config.ServerUrl))
        {
            result.AddError("Sonarr.ServerUrl", "Sonarr server URL is not a valid URL");
        }
        else
        {
            // Test Sonarr API connectivity
            await ValidateSonarrApiAsync(config, result);
        }

        // Validate API key
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            result.AddError("Sonarr.ApiKey", "Sonarr API key is required");
        }
        else if (config.ApiKey.Length != 32)
        {
            result.AddWarning("Sonarr.ApiKey", "Sonarr API key should be 32 characters long");
        }

        // Validate quality profile
        if (string.IsNullOrWhiteSpace(config.QualityProfile))
        {
            result.AddWarning("Sonarr.QualityProfile", "Sonarr quality profile is not set - will use default");
        }
    }

    /// <summary>
    /// Validates Radarr configuration.
    /// </summary>
    /// <param name="config">The Radarr configuration.</param>
    /// <param name="result">The validation result.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ValidateRadarrConfigurationAsync(RadarrConfiguration config, ConfigurationValidationResult result)
    {
        if (config == null)
        {
            result.AddWarning("Radarr", "Radarr configuration is not set - movie search will be limited");
            return;
        }

        // Validate server URL
        if (string.IsNullOrWhiteSpace(config.ServerUrl))
        {
            result.AddError("Radarr.ServerUrl", "Radarr server URL is required");
        }
        else if (!IsValidUrl(config.ServerUrl))
        {
            result.AddError("Radarr.ServerUrl", "Radarr server URL is not a valid URL");
        }
        else
        {
            // Test Radarr API connectivity
            await ValidateRadarrApiAsync(config, result);
        }

        // Validate API key
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            result.AddError("Radarr.ApiKey", "Radarr API key is required");
        }
        else if (config.ApiKey.Length != 32)
        {
            result.AddWarning("Radarr.ApiKey", "Radarr API key should be 32 characters long");
        }

        // Validate quality profile
        if (string.IsNullOrWhiteSpace(config.QualityProfile))
        {
            result.AddWarning("Radarr.QualityProfile", "Radarr quality profile is not set - will use default");
        }
    }

    /// <summary>
    /// Validates general plugin settings.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="result">The validation result.</param>
    private void ValidateGeneralSettings(PluginConfiguration config, ConfigurationValidationResult result)
    {
        // Validate cache settings
        if (config.CacheExpirationMinutes <= 0)
        {
            result.AddError("CacheExpirationMinutes", "Cache expiration must be greater than 0");
        }
        else if (config.CacheExpirationMinutes > 1440) // 24 hours
        {
            result.AddWarning("CacheExpirationMinutes", "Cache expiration is very high (>24 hours)");
        }

        // Validate concurrent requests
        if (config.MaxConcurrentRequests <= 0)
        {
            result.AddError("MaxConcurrentRequests", "Max concurrent requests must be greater than 0");
        }
        else if (config.MaxConcurrentRequests > 20)
        {
            result.AddWarning("MaxConcurrentRequests", "Max concurrent requests is very high (>20)");
        }

        // Validate rate limiting
        if (config.EnableRateLimiting && config.RequestsPerSecond <= 0)
        {
            result.AddError("RequestsPerSecond", "Requests per second must be greater than 0 when rate limiting is enabled");
        }
    }

    /// <summary>
    /// Validates Sonarr API connectivity.
    /// </summary>
    /// <param name="config">The Sonarr configuration.</param>
    /// <param name="result">The validation result.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ValidateSonarrApiAsync(SonarrConfiguration config, ConfigurationValidationResult result)
    {
        try
        {
            var cacheKey = $"sonarr_validation_{config.ServerUrl.GetHashCode()}_{config.ApiKey.GetHashCode()}";
            var cachedResult = _cacheService.Get<object>(cacheKey);
            bool isValid;
            
            if (cachedResult == null)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"{config.ServerUrl.TrimEnd('/')}/api/v3/system/status");
                request.Headers.Add("X-Api-Key", config.ApiKey);
                
                var response = await _httpClient.SendAsync(request);
                isValid = response.IsSuccessStatusCode;
                
                _cacheService.Set(cacheKey, isValid, TimeSpan.FromMinutes(15));
            }
            else
            {
                isValid = (bool)cachedResult;
            }

            if (isValid)
            {
                result.AddSuccess("Sonarr.Connection", "Sonarr API is accessible");
            }
            else
            {
                result.AddError("Sonarr.Connection", "Sonarr API is not accessible - check URL and API key");
            }
        }
        catch (Exception ex)
        {
            result.AddError("Sonarr.Connection", $"Failed to connect to Sonarr API: {ex.Message}");
            _logger.LogError(ex, "Error validating Sonarr API");
        }
    }

    /// <summary>
    /// Validates Radarr API connectivity.
    /// </summary>
    /// <param name="config">The Radarr configuration.</param>
    /// <param name="result">The validation result.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ValidateRadarrApiAsync(RadarrConfiguration config, ConfigurationValidationResult result)
    {
        try
        {
            var cacheKey = $"radarr_validation_{config.ServerUrl.GetHashCode()}_{config.ApiKey.GetHashCode()}";
            var cachedResult = _cacheService.Get<object>(cacheKey);
            bool isValid;
            
            if (cachedResult == null)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"{config.ServerUrl.TrimEnd('/')}/api/v3/system/status");
                request.Headers.Add("X-Api-Key", config.ApiKey);
                
                var response = await _httpClient.SendAsync(request);
                isValid = response.IsSuccessStatusCode;
                
                _cacheService.Set(cacheKey, isValid, TimeSpan.FromMinutes(15));
            }
            else
            {
                isValid = (bool)cachedResult;
            }

            if (isValid)
            {
                result.AddSuccess("Radarr.Connection", "Radarr API is accessible");
            }
            else
            {
                result.AddError("Radarr.Connection", "Radarr API is not accessible - check URL and API key");
            }
        }
        catch (Exception ex)
        {
            result.AddError("Radarr.Connection", $"Failed to connect to Radarr API: {ex.Message}");
            _logger.LogError(ex, "Error validating Radarr API");
        }
    }

    /// <summary>
    /// Validates if a string is a valid URL.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if the URL is valid.</returns>
    private bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            var uri = new Uri(url);
            return uri.Scheme == "http" || uri.Scheme == "https";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets configuration recommendations.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>Configuration recommendations.</returns>
    public List<ConfigurationRecommendation> GetRecommendations(PluginConfiguration config)
    {
        var recommendations = new List<ConfigurationRecommendation>();

        if (config == null)
        {
            return recommendations;
        }

        // Performance recommendations
        if (config.CacheExpirationMinutes < 60)
        {
            recommendations.Add(new ConfigurationRecommendation
            {
                Category = "Performance",
                Field = "CacheExpirationMinutes",
                Message = "Consider increasing cache expiration to reduce API calls",
                Severity = RecommendationSeverity.Low,
                RecommendedValue = "60"
            });
        }

        if (config.MaxConcurrentRequests < 5)
        {
            recommendations.Add(new ConfigurationRecommendation
            {
                Category = "Performance",
                Field = "MaxConcurrentRequests",
                Message = "Consider increasing concurrent requests for better performance",
                Severity = RecommendationSeverity.Low,
                RecommendedValue = "5"
            });
        }

        // Security recommendations
        if (!config.EnableRateLimiting)
        {
            recommendations.Add(new ConfigurationRecommendation
            {
                Category = "Security",
                Field = "EnableRateLimiting",
                Message = "Enable rate limiting to prevent API abuse",
                Severity = RecommendationSeverity.Medium,
                RecommendedValue = "true"
            });
        }

        // Feature recommendations
        if (config.Sonarr == null)
        {
            recommendations.Add(new ConfigurationRecommendation
            {
                Category = "Features",
                Field = "Sonarr",
                Message = "Configure Sonarr for TV show torrent search",
                Severity = RecommendationSeverity.Medium,
                RecommendedValue = "Configure Sonarr integration"
            });
        }

        if (config.Radarr == null)
        {
            recommendations.Add(new ConfigurationRecommendation
            {
                Category = "Features",
                Field = "Radarr",
                Message = "Configure Radarr for movie torrent search",
                Severity = RecommendationSeverity.Medium,
                RecommendedValue = "Configure Radarr integration"
            });
        }

        return recommendations;
    }
}

/// <summary>
/// Configuration validation result.
/// </summary>
public class ConfigurationValidationResult
{
    /// <summary>
    /// Gets or sets whether the configuration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public List<ValidationMessage> Errors { get; } = new();

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public List<ValidationMessage> Warnings { get; } = new();

    /// <summary>
    /// Gets the validation success messages.
    /// </summary>
    public List<ValidationMessage> Successes { get; } = new();

    /// <summary>
    /// Adds an error message.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="message">The error message.</param>
    public void AddError(string field, string message)
    {
        Errors.Add(new ValidationMessage { Field = field, Message = message });
    }

    /// <summary>
    /// Adds a warning message.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="message">The warning message.</param>
    public void AddWarning(string field, string message)
    {
        Warnings.Add(new ValidationMessage { Field = field, Message = message });
    }

    /// <summary>
    /// Adds a success message.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="message">The success message.</param>
    public void AddSuccess(string field, string message)
    {
        Successes.Add(new ValidationMessage { Field = field, Message = message });
    }
}

/// <summary>
/// Validation message.
/// </summary>
public class ValidationMessage
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Configuration recommendation.
/// </summary>
public class ConfigurationRecommendation
{
    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recommendation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recommendation severity.
    /// </summary>
    public RecommendationSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the recommended value.
    /// </summary>
    public string RecommendedValue { get; set; } = string.Empty;
}

/// <summary>
/// Recommendation severity levels.
/// </summary>
public enum RecommendationSeverity
{
    /// <summary>
    /// Low severity recommendation.
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity recommendation.
    /// </summary>
    Medium,

    /// <summary>
    /// High severity recommendation.
    /// </summary>
    High
} 