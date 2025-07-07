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
    private const string DefaultLanguage = "en-US";
    private const string DefaultRegion = "US";
    
    // Dynamic image configuration
    private string? _imageBaseUrl;
    private string[]? _posterSizes;
    private string[]? _backdropSizes;
    
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
        
        // Initialize image configuration
        _ = InitializeImageConfigurationAsync();
    }

    /// <summary>
    /// Initializes the image configuration from TMDB.
    /// </summary>
    private async Task InitializeImageConfigurationAsync()
    {
        try
        {
            var cachedConfig = await _cacheService.GetOrCreateAsync("tmdb_image_config", async () =>
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/configuration?api_key={_apiKey}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var config = JObject.Parse(content);
                    return new
                    {
                        BaseUrl = config["images"]?["secure_base_url"]?.Value<string>() ?? "https://image.tmdb.org/t/p/",
                                                 PosterSizes = config["images"]?["poster_sizes"]?.Values<string>().Where(s => !string.IsNullOrEmpty(s)).ToArray() ?? new[] { "w500" },
                         BackdropSizes = config["images"]?["backdrop_sizes"]?.Values<string>().Where(s => !string.IsNullOrEmpty(s)).ToArray() ?? new[] { "w1280" }
                    };
                }
                return null;
            }, TimeSpan.FromDays(7)); // Cache for a week

            if (cachedConfig != null)
            {
                _imageBaseUrl = cachedConfig.BaseUrl;
                _posterSizes = cachedConfig.PosterSizes;
                _backdropSizes = cachedConfig.BackdropSizes;
            }
            else
            {
                // Fallback to defaults
                _imageBaseUrl = "https://image.tmdb.org/t/p/";
                _posterSizes = new[] { "w500" };
                _backdropSizes = new[] { "w1280" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TMDB image configuration, using defaults");
            _imageBaseUrl = "https://image.tmdb.org/t/p/";
            _posterSizes = new[] { "w500" };
            _backdropSizes = new[] { "w1280" };
        }
    }

    /// <summary>
    /// Gets the appropriate poster URL for the given path.
    /// </summary>
    /// <param name="posterPath">The poster path from TMDB.</param>
    /// <param name="size">The desired size (defaults to w500).</param>
    /// <returns>The full poster URL.</returns>
    public string? GetPosterUrl(string? posterPath, string size = "w500")
    {
        if (string.IsNullOrEmpty(posterPath) || string.IsNullOrEmpty(_imageBaseUrl))
            return null;
            
        // Ensure size is available
        if (_posterSizes?.Contains(size) != true)
            size = "w500";
            
        return $"{_imageBaseUrl}{size}{posterPath}";
    }

    /// <summary>
    /// Gets the appropriate backdrop URL for the given path.
    /// </summary>
    /// <param name="backdropPath">The backdrop path from TMDB.</param>
    /// <param name="size">The desired size (defaults to w1280).</param>
    /// <returns>The full backdrop URL.</returns>
    public string? GetBackdropUrl(string? backdropPath, string size = "w1280")
    {
        if (string.IsNullOrEmpty(backdropPath) || string.IsNullOrEmpty(_imageBaseUrl))
            return null;
            
        // Ensure size is available
        if (_backdropSizes?.Contains(size) != true)
            size = "w1280";
            
        return $"{_imageBaseUrl}{size}{backdropPath}";
    }

    /// <summary>
    /// Searches for movies and TV shows on TMDB.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="page">The page number (default: 1).</param>
    /// <param name="language">The language (default: en-US).</param>
    /// <param name="region">The region (default: US).</param>
    /// <returns>A collection of search results.</returns>
    public async Task<IEnumerable<JObject>> SearchAsync(string query, int page = 1, string language = DefaultLanguage, string region = DefaultRegion)
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
        var cacheKey = $"tmdb_search_{query}_{page}_{language}_{region}";

        try
        {
            // Try to get from cache first
            var cachedResults = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Searching TMDB for: {Query} (Page {Page}, Language: {Language}, Region: {Region})", query, page, language, region);
                
                var results = new List<JObject>();
                
                // Search movies
                var movieResults = await SearchMoviesAsync(query, page, language, region);
                results.AddRange(movieResults);
                
                // Search TV shows
                var tvResults = await SearchTVShowsAsync(query, page, language, region);
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
    /// Gets trending movies and TV shows.
    /// </summary>
    /// <param name="mediaType">The media type (all, movie, tv).</param>
    /// <param name="timeWindow">The time window (day, week).</param>
    /// <param name="language">The language (default: en-US).</param>
    /// <returns>A collection of trending results.</returns>
    public async Task<IEnumerable<JObject>> GetTrendingAsync(string mediaType = "all", string timeWindow = "week", string language = DefaultLanguage)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("TMDB API key is not configured");
            return Enumerable.Empty<JObject>();
        }

        var cacheKey = $"tmdb_trending_{mediaType}_{timeWindow}_{language}";

        try
        {
            var cachedResults = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Getting trending {MediaType} content for {TimeWindow}", mediaType, timeWindow);
                
                var url = $"{BaseUrl}/trending/{mediaType}/{timeWindow}?api_key={_apiKey}&language={language}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get trending content. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<JObject>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(content);
                var results = result["results"]?.ToObject<JArray>() ?? new JArray();
                
                return results.Cast<JObject>().ToList();
            }, TimeSpan.FromHours(1)); // Cache trending for 1 hour

            return cachedResults ?? Enumerable.Empty<JObject>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trending content");
            return Enumerable.Empty<JObject>();
        }
    }

    /// <summary>
    /// Gets popular movies.
    /// </summary>
    /// <param name="page">The page number (default: 1).</param>
    /// <param name="language">The language (default: en-US).</param>
    /// <param name="region">The region (default: US).</param>
    /// <returns>A collection of popular movies.</returns>
    public async Task<IEnumerable<JObject>> GetPopularMoviesAsync(int page = 1, string language = DefaultLanguage, string region = DefaultRegion)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("TMDB API key is not configured");
            return Enumerable.Empty<JObject>();
        }

        var cacheKey = $"tmdb_popular_movies_{page}_{language}_{region}";

        try
        {
            var cachedResults = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Getting popular movies (Page {Page}, Language: {Language}, Region: {Region})", page, language, region);
                
                var url = $"{BaseUrl}/movie/popular?api_key={_apiKey}&page={page}&language={language}&region={region}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get popular movies. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<JObject>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(content);
                var results = result["results"]?.ToObject<JArray>() ?? new JArray();
                
                return results.Cast<JObject>().Select(movie =>
                {
                    movie["media_type"] = "movie";
                    return movie;
                }).ToList();
            }, TimeSpan.FromHours(6)); // Cache popular content for 6 hours

            return cachedResults ?? Enumerable.Empty<JObject>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular movies");
            return Enumerable.Empty<JObject>();
        }
    }

    /// <summary>
    /// Gets popular TV shows.
    /// </summary>
    /// <param name="page">The page number (default: 1).</param>
    /// <param name="language">The language (default: en-US).</param>
    /// <returns>A collection of popular TV shows.</returns>
    public async Task<IEnumerable<JObject>> GetPopularTVShowsAsync(int page = 1, string language = DefaultLanguage)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("TMDB API key is not configured");
            return Enumerable.Empty<JObject>();
        }

        var cacheKey = $"tmdb_popular_tv_{page}_{language}";

        try
        {
            var cachedResults = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Getting popular TV shows (Page {Page}, Language: {Language})", page, language);
                
                var url = $"{BaseUrl}/tv/popular?api_key={_apiKey}&page={page}&language={language}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get popular TV shows. Status: {StatusCode}", response.StatusCode);
                    return Enumerable.Empty<JObject>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(content);
                var results = result["results"]?.ToObject<JArray>() ?? new JArray();
                
                return results.Cast<JObject>().Select(tv =>
                {
                    tv["media_type"] = "tv";
                    return tv;
                }).ToList();
            }, TimeSpan.FromHours(6)); // Cache popular content for 6 hours

            return cachedResults ?? Enumerable.Empty<JObject>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular TV shows");
            return Enumerable.Empty<JObject>();
        }
    }

    /// <summary>
    /// Gets detailed information about a movie.
    /// </summary>
    /// <param name="tmdbId">The TMDB movie ID.</param>
    /// <param name="language">The language (default: en-US).</param>
    /// <returns>Detailed movie information.</returns>
    public async Task<JObject?> GetMovieDetailsAsync(int tmdbId, string language = DefaultLanguage)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("TMDB API key is not configured");
            return null;
        }

        // Create cache key
        var cacheKey = $"tmdb_movie_details_{tmdbId}_{language}";

        try
        {
            // Try to get from cache first
            var cachedMovie = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching movie details for ID: {TmdbId} (Language: {Language})", tmdbId, language);
                
                var url = $"{BaseUrl}/movie/{tmdbId}?api_key={_apiKey}&language={language}&append_to_response=credits,videos,external_ids,recommendations";
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
    /// <param name="language">The language (default: en-US).</param>
    /// <returns>Detailed TV show information.</returns>
    public async Task<JObject?> GetTVShowDetailsAsync(int tmdbId, string language = DefaultLanguage)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogError("TMDB API key is not configured");
            return null;
        }

        // Create cache key
        var cacheKey = $"tmdb_tv_details_{tmdbId}_{language}";

        try
        {
            // Try to get from cache first
            var cachedTVShow = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching TV show details for ID: {TmdbId} (Language: {Language})", tmdbId, language);
                
                var url = $"{BaseUrl}/tv/{tmdbId}?api_key={_apiKey}&language={language}&append_to_response=credits,videos,external_ids,recommendations";
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

    private async Task<IEnumerable<JObject>> SearchMoviesAsync(string query, int page, string language, string region)
    {
        var url = $"{BaseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query)}&page={page}&language={language}&region={region}";
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

    private async Task<IEnumerable<JObject>> SearchTVShowsAsync(string query, int page, string language, string region)
    {
        var url = $"{BaseUrl}/search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(query)}&page={page}&language={language}";
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