using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyScout.Services;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellyScout;

/// <summary>
/// Core service for JellyScout functionality.
/// </summary>
public class CatalogModule
{
    private readonly ILogger<CatalogModule> _logger;
    private readonly TMDBService _tmdbService;
    private readonly JellyfinLibraryChecker _libraryChecker;
    private readonly StreamingService _streamingService;
    private readonly NotificationService _notificationService;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogModule"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="tmdbService">The TMDB service.</param>
    /// <param name="libraryChecker">The library checker.</param>
    /// <param name="streamingService">The streaming service.</param>
    /// <param name="notificationService">The notification service.</param>
    /// <param name="libraryManager">The library manager.</param>
    public CatalogModule(
        ILogger<CatalogModule> logger,
        TMDBService tmdbService,
        JellyfinLibraryChecker libraryChecker,
        StreamingService streamingService,
        NotificationService notificationService,
        ILibraryManager libraryManager)
    {
        _logger = logger;
        _tmdbService = tmdbService;
        _libraryChecker = libraryChecker;
        _streamingService = streamingService;
        _notificationService = notificationService;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Searches for movies and TV shows.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="page">The page number.</param>
    /// <param name="maxResults">The maximum results.</param>
    /// <returns>Search results.</returns>
    public async Task<object> SearchAsync(string query, int page = 1, int maxResults = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new { error = "Query parameter is required." };
            }

            if (query.Length > 100)
            {
                return new { error = "Query must be 100 characters or less." };
            }

            if (page < 1)
            {
                return new { error = "Page must be greater than 0." };
            }

            if (maxResults < 1 || maxResults > 100)
            {
                return new { error = "MaxResults must be between 1 and 100." };
            }

            _logger.LogInformation("JellyScout: Searching for '{Query}' (page {Page}, max {MaxResults})", query, page, maxResults);

            var tmdbResults = await _tmdbService.SearchAsync(query, page);

            var results = new List<object>();

            foreach (var result in tmdbResults.Take(maxResults))
            {
                var title = result.Value<string>("title") ?? result.Value<string>("name");
                var yearStr = result.Value<string>("release_date")?.Split('-')[0] ?? 
                              result.Value<string>("first_air_date")?.Split('-')[0];
                var year = int.TryParse(yearStr, out var y) ? y : (int?)null;
                var mediaType = result.Value<string>("media_type") ?? "unknown";
                var tmdbId = result.Value<int?>("id");
                var overview = result.Value<string>("overview") ?? "";
                var posterPath = result.Value<string>("poster_path");
                var backdropPath = result.Value<string>("backdrop_path");
                var popularity = result.Value<double?>("popularity") ?? 0;
                var voteAverage = result.Value<double?>("vote_average") ?? 0;

                var alreadyInLibrary = false;
                if (!string.IsNullOrEmpty(title) && Plugin.Instance?.Configuration?.AutoCheckLibrary == true)
                {
                    alreadyInLibrary = await _libraryChecker.ExistsAsync(title, year);
                }

                var resultObj = new
                {
                    title,
                    year,
                    tmdbId,
                    mediaType,
                    overview,
                    posterPath = !string.IsNullOrEmpty(posterPath) ? $"https://image.tmdb.org/t/p/w500{posterPath}" : null,
                    backdropPath = !string.IsNullOrEmpty(backdropPath) ? $"https://image.tmdb.org/t/p/w1280{backdropPath}" : null,
                    popularity,
                    voteAverage,
                    alreadyInLibrary
                };

                results.Add(resultObj);
            }

            return new
            {
                query,
                page,
                results = results.ToArray(),
                totalResults = results.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for '{Query}'", query);
            return new { error = "An error occurred while searching. Please try again." };
        }
    }

    /// <summary>
    /// Gets plugin configuration status.
    /// </summary>
    /// <returns>Configuration status.</returns>
    public object GetStatus()
    {
        try
        {
            var config = Plugin.Instance?.Configuration;
            var hasApiKey = !string.IsNullOrWhiteSpace(config?.TmdbApiKey);

            return new
            {
                configured = hasApiKey,
                streamingEnabled = config?.EnableStreaming ?? false,
                downloadsEnabled = config?.EnableDownloads ?? false,
                notificationsEnabled = config?.EnableNotifications ?? false,
                autoCheckLibrary = config?.AutoCheckLibrary ?? false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plugin status");
            return new { error = "An error occurred while getting status." };
        }
    }
}
