using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for interacting with The Movie Database (TMDB) API.
/// </summary>
public class TMDBService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TMDBService> _logger;
    private readonly CacheService _cacheService;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.themoviedb.org/3";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="TMDBService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cacheService">The cache service.</param>
    public TMDBService(HttpClient httpClient, ILogger<TMDBService> logger, CacheService cacheService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
        _apiKey = GetApiKey();
        
        // Configure HttpClient
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "JellyfinJellyScout/2.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Searches for movies and TV shows on TMDB.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="page">The page number (default: 1).</param>
    /// <returns>A collection of search results.</returns>
    public async Task<IEnumerable<JObject>> SearchAsync(string query, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("Search query is null or empty");
            return Enumerable.Empty<JObject>();
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("TMDB API key is not configured");
            return Enumerable.Empty<JObject>();
        }

        // Create cache key
        var cacheKey = $"tmdb_search_{query}_{page}";

        try
        {
            // Try to get from cache first
            var cachedResults = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Searching TMDB for: {Query} (Page {Page})", query, page);
                
                var results = new List<JObject>();
                
                // Search movies
                var movieResults = await SearchMoviesAsync(query, page);
                results.AddRange(movieResults);
                
                // Search TV shows
                var tvResults = await SearchTVShowsAsync(query, page);
                results.AddRange(tvResults);
                
                // Sort by popularity (descending)
                return results.OrderByDescending(r => r.Value<double?>("popularity") ?? 0).ToList();
            }, TimeSpan.FromMinutes(15)); // Cache search results for 15 minutes

            return cachedResults ?? Enumerable.Empty<JObject>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while searching TMDB for query: {Query}", query);
            return Enumerable.Empty<JObject>();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout while searching TMDB for query: {Query}", query);
            return Enumerable.Empty<JObject>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while searching TMDB for query: {Query}", query);
            return Enumerable.Empty<JObject>();
        }
    }

    /// <summary>
    /// Gets detailed information about a movie.
    /// </summary>
    /// <param name="tmdbId">The TMDB movie ID.</param>
    /// <returns>Detailed movie information.</returns>
    public async Task<JObject?> GetMovieDetailsAsync(int tmdbId)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("TMDB API key is not configured");
            return null;
        }

        // Create cache key
        var cacheKey = $"tmdb_movie_details_{tmdbId}";

        try
        {
            // Try to get from cache first
            var cachedMovie = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching movie details for ID: {TmdbId}", tmdbId);
                
                var url = $"{BaseUrl}/movie/{tmdbId}?api_key={_apiKey}&append_to_response=credits,videos,external_ids";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get movie details for ID {TmdbId}. Status: {StatusCode}", tmdbId, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var movie = JObject.Parse(content);
                movie["media_type"] = "movie";
                
                return movie;
            }, TimeSpan.FromHours(4)); // Cache movie details for 4 hours

            return cachedMovie;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting movie details for ID {TmdbId}", tmdbId);
            return null;
        }
    }

    /// <summary>
    /// Gets detailed information about a TV show.
    /// </summary>
    /// <param name="tmdbId">The TMDB TV show ID.</param>
    /// <returns>Detailed TV show information.</returns>
    public async Task<JObject?> GetTVShowDetailsAsync(int tmdbId)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("TMDB API key is not configured");
            return null;
        }

        // Create cache key
        var cacheKey = $"tmdb_tv_details_{tmdbId}";

        try
        {
            // Try to get from cache first
            var cachedTVShow = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching TV show details for ID: {TmdbId}", tmdbId);
                
                var url = $"{BaseUrl}/tv/{tmdbId}?api_key={_apiKey}&append_to_response=credits,videos,external_ids";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get TV show details for ID {TmdbId}. Status: {StatusCode}", tmdbId, response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var tvShow = JObject.Parse(content);
                tvShow["media_type"] = "tv";
                
                return tvShow;
            }, TimeSpan.FromHours(4)); // Cache TV show details for 4 hours

            return cachedTVShow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TV show details for ID {TmdbId}", tmdbId);
            return null;
        }
    }

    private async Task<IEnumerable<JObject>> SearchMoviesAsync(string query, int page)
    {
        var url = $"{BaseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query)}&page={page}";
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to search movies. Status: {StatusCode}", response.StatusCode);
            return Enumerable.Empty<JObject>();
        }

        var content = await response.Content.ReadAsStringAsync();
        var searchResult = JObject.Parse(content);
        var results = searchResult["results"]?.ToObject<JArray>() ?? new JArray();
        
        return results.Cast<JObject>().Select(movie =>
        {
            movie["media_type"] = "movie";
            return movie;
        });
    }

    private async Task<IEnumerable<JObject>> SearchTVShowsAsync(string query, int page)
    {
        var url = $"{BaseUrl}/search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(query)}&page={page}";
        var response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to search TV shows. Status: {StatusCode}", response.StatusCode);
            return Enumerable.Empty<JObject>();
        }

        var content = await response.Content.ReadAsStringAsync();
        var searchResult = JObject.Parse(content);
        var results = searchResult["results"]?.ToObject<JArray>() ?? new JArray();
        
        return results.Cast<JObject>().Select(tvShow =>
        {
            tvShow["media_type"] = "tv";
            return tvShow;
        });
    }

    private string GetApiKey()
    {
        // Try to get from environment variable first
        var apiKey = Environment.GetEnvironmentVariable("TMDB_API_KEY");
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Try to get from plugin configuration
            apiKey = Plugin.Instance?.Configuration?.TmdbApiKey;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("TMDB API key not found. Please set TMDB_API_KEY environment variable or configure it in plugin settings.");
            return string.Empty;
        }

        return apiKey;
    }
} 