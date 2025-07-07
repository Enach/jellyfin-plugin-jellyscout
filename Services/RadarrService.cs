using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyScout.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for integrating with Radarr for movie torrent searching.
/// </summary>
public class RadarrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RadarrService> _logger;
    private RadarrConfiguration? _radarrConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="RadarrService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    public RadarrService(HttpClient httpClient, ILogger<RadarrService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sets the Radarr configuration.
    /// </summary>
    /// <param name="config">The Radarr configuration.</param>
    public void SetRadarrConfiguration(RadarrConfiguration config)
    {
        _radarrConfig = config;
        _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    }

    /// <summary>
    /// Searches for movie torrents using Radarr.
    /// </summary>
    /// <param name="title">The movie title.</param>
    /// <param name="year">The release year.</param>
    /// <param name="quality">The preferred quality.</param>
    /// <returns>A collection of torrent results.</returns>
    public async Task<IEnumerable<TorrentResult>> SearchMovieTorrentsAsync(
        string title, 
        int? year = null, 
        string quality = "1080p")
    {
        try
        {
            if (_radarrConfig == null || !_radarrConfig.Enabled)
            {
                _logger.LogWarning("Radarr is not configured or disabled");
                return Enumerable.Empty<TorrentResult>();
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                _logger.LogWarning("Title is required for Radarr search");
                return Enumerable.Empty<TorrentResult>();
            }

            _logger.LogInformation("Searching Radarr for movie: {Title} ({Year})", title, year);

            // First, search for the movie
            var movieId = await FindMovieIdAsync(title, year);
            if (movieId == null)
            {
                _logger.LogWarning("Could not find movie ID for: {Title} ({Year})", title, year);
                return Enumerable.Empty<TorrentResult>();
            }

            // Search for releases
            var releases = await SearchReleasesAsync(movieId.Value, quality);
            
            return releases.Select(ConvertToTorrentResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Radarr for movie: {Title} ({Year})", title, year);
            return Enumerable.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Triggers a movie search in Radarr and returns available releases.
    /// </summary>
    /// <param name="title">The movie title.</param>
    /// <param name="year">The release year.</param>
    /// <param name="quality">The preferred quality.</param>
    /// <returns>A collection of torrent results.</returns>
    public async Task<IEnumerable<TorrentResult>> TriggerMovieSearchAsync(
        string title, 
        int? year = null, 
        string quality = "1080p")
    {
        try
        {
            if (_radarrConfig == null || !_radarrConfig.Enabled)
            {
                _logger.LogWarning("Radarr is not configured or disabled");
                return Enumerable.Empty<TorrentResult>();
            }

            _logger.LogInformation("Triggering Radarr movie search for: {Title} ({Year})", title, year);

            var movieId = await FindMovieIdAsync(title, year);
            if (movieId == null)
            {
                _logger.LogWarning("Could not find movie ID for: {Title} ({Year})", title, year);
                return Enumerable.Empty<TorrentResult>();
            }

            // Trigger movie search
            var searchRequest = new
            {
                name = "MoviesSearch",
                movieIds = new[] { movieId.Value }
            };

            var jsonContent = JsonConvert.SerializeObject(searchRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _radarrConfig.ApiKey);

            var response = await _httpClient.PostAsync($"{_radarrConfig.ServerUrl}/api/v3/command", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Movie search command sent successfully");
                // Wait a moment for search to process
                await Task.Delay(2000);
                
                // Get the search results
                return await SearchReleasesAsync(movieId.Value, quality).ContinueWith(t => 
                    t.Result.Select(ConvertToTorrentResult));
            }
            else
            {
                _logger.LogError("Radarr API error: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            }

            return Enumerable.Empty<TorrentResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering Radarr movie search for: {Title} ({Year})", title, year);
            return Enumerable.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Gets available releases for a movie from Radarr.
    /// </summary>
    /// <param name="movieId">The Radarr movie ID.</param>
    /// <param name="quality">The preferred quality.</param>
    /// <returns>A collection of releases.</returns>
    public async Task<IEnumerable<TorrentResult>> GetMovieReleasesAsync(int movieId, string quality = "1080p")
    {
        try
        {
            var releases = await SearchReleasesAsync(movieId, quality);
            return releases.Select(ConvertToTorrentResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting movie releases for movie ID: {MovieId}", movieId);
            return Enumerable.Empty<TorrentResult>();
        }
    }

    private async Task<int?> FindMovieIdAsync(string title, int? year = null)
    {
        if (_radarrConfig == null)
        {
            _logger.LogWarning("Radarr configuration is not set");
            return null;
        }

        try
        {
            var searchUrl = $"{_radarrConfig.ServerUrl}/api/v3/movie/lookup?term={Uri.EscapeDataString(title)}";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _radarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(searchUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var movies = JsonConvert.DeserializeObject<RadarrMovie[]>(content);

                if (movies != null && movies.Length > 0)
                {
                    // Find the best match
                    var bestMatch = movies.FirstOrDefault(m => 
                        m.Title.Equals(title, StringComparison.OrdinalIgnoreCase) ||
                        (year.HasValue && m.Year == year.Value));

                    if (bestMatch != null)
                    {
                        // Check if movie is already in Radarr, if not add it
                        if (bestMatch.Id == 0)
                        {
                            return await AddMovieToRadarrAsync(bestMatch);
                        }
                        return bestMatch.Id;
                    }

                    // If no exact match, return the first result
                    return movies[0].Id != 0 ? movies[0].Id : await AddMovieToRadarrAsync(movies[0]);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding movie ID for: {Title} ({Year})", title, year);
            return null;
        }
    }

    private async Task<int?> AddMovieToRadarrAsync(RadarrMovie movie)
    {
        try
        {
            _logger.LogInformation("Adding movie to Radarr: {Title} ({Year})", movie.Title, movie.Year);

            if (_radarrConfig == null)
            {
                _logger.LogWarning("Radarr configuration is not set");
                return null;
            }

            // Prepare movie for adding to Radarr
            var addMovieRequest = new
            {
                title = movie.Title,
                year = movie.Year,
                qualityProfileId = _radarrConfig.QualityProfileId,
                titleSlug = movie.TitleSlug,
                images = movie.Images,
                tmdbId = movie.TmdbId,
                monitored = true,
                rootFolderPath = "/movies", // This should be configurable
                addOptions = new
                {
                    searchForMovie = true
                }
            };

            var jsonContent = JsonConvert.SerializeObject(addMovieRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _radarrConfig.ApiKey);

            var response = await _httpClient.PostAsync($"{_radarrConfig.ServerUrl}/api/v3/movie", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var addedMovie = JsonConvert.DeserializeObject<RadarrMovie>(responseContent);
                
                _logger.LogInformation("Movie added to Radarr successfully: {Title} (ID: {Id})", movie.Title, addedMovie?.Id);
                return addedMovie?.Id;
            }
            else
            {
                _logger.LogError("Failed to add movie to Radarr: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding movie to Radarr: {Title} ({Year})", movie.Title, movie.Year);
            return null;
        }
    }

    private async Task<IEnumerable<RadarrRelease>> SearchReleasesAsync(int movieId, string quality = "1080p")
    {
        if (_radarrConfig == null)
        {
            _logger.LogWarning("Radarr configuration is not set");
            return Enumerable.Empty<RadarrRelease>();
        }

        try
        {
            var releaseUrl = $"{_radarrConfig.ServerUrl}/api/v3/release?movieId={movieId}";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _radarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(releaseUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var releases = JsonConvert.DeserializeObject<RadarrRelease[]>(content);

                if (releases != null)
                {
                    // Filter by quality if specified
                    var filteredReleases = releases.AsEnumerable();
                    
                    if (!string.IsNullOrEmpty(quality))
                    {
                        filteredReleases = filteredReleases.Where(r => 
                            r.Title.Contains(quality, StringComparison.OrdinalIgnoreCase));
                    }

                    return filteredReleases
                        .OrderByDescending(r => r.SeedsNumber)
                        .ThenByDescending(r => GetQualityScore(quality))
                        .Take(10);
                }
            }
            else
            {
                _logger.LogWarning("No releases found for movie ID: {MovieId}", movieId);
            }

            return Enumerable.Empty<RadarrRelease>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching releases for movie ID: {MovieId}", movieId);
            return Enumerable.Empty<RadarrRelease>();
        }
    }

    private static TorrentResult ConvertToTorrentResult(RadarrRelease release)
    {
        return new TorrentResult
        {
            Name = release.Title,
            Size = FormatFileSize(release.Size),
            Seeders = release.SeedsNumber,
            Leechers = release.LeechersNumber,
            Quality = ExtractQuality(release.Title),
            Provider = "Radarr",
            MagnetLink = release.MagnetUrl ?? string.Empty,
            UploadDate = release.PublishDate
        };
    }

    private static string FormatFileSize(long sizeInBytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = sizeInBytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static string ExtractQuality(string title)
    {
        if (title.Contains("2160p", StringComparison.OrdinalIgnoreCase) || title.Contains("4K", StringComparison.OrdinalIgnoreCase))
            return "4K";
        if (title.Contains("1080p", StringComparison.OrdinalIgnoreCase))
            return "1080p";
        if (title.Contains("720p", StringComparison.OrdinalIgnoreCase))
            return "720p";
        if (title.Contains("480p", StringComparison.OrdinalIgnoreCase))
            return "480p";
        
        return "Unknown";
    }

    private static int GetQualityScore(string quality)
    {
        return quality?.ToLowerInvariant() switch
        {
            "4k" or "2160p" => 100,
            "1080p" => 90,
            "720p" => 80,
            "480p" => 70,
            _ => 50
        };
    }
}

/// <summary>
/// Represents a Radarr movie.
/// </summary>
public class RadarrMovie
{
    /// <summary>
    /// Gets or sets the movie ID.
    /// </summary>
    [JsonProperty("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    [JsonProperty("year")]
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the TMDB ID.
    /// </summary>
    [JsonProperty("tmdbId")]
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the title slug.
    /// </summary>
    [JsonProperty("titleSlug")]
    public string TitleSlug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the images.
    /// </summary>
    [JsonProperty("images")]
    public RadarrImage[]? Images { get; set; }
}

/// <summary>
/// Represents a Radarr image.
/// </summary>
public class RadarrImage
{
    /// <summary>
    /// Gets or sets the cover type.
    /// </summary>
    [JsonProperty("coverType")]
    public string CoverType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL.
    /// </summary>
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Represents a Radarr release.
/// </summary>
public class RadarrRelease
{
    /// <summary>
    /// Gets or sets the release title.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the magnet URL.
    /// </summary>
    [JsonProperty("magnetUrl")]
    public string? MagnetUrl { get; set; }

    /// <summary>
    /// Gets or sets the file size.
    /// </summary>
    [JsonProperty("size")]
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the number of seeders.
    /// </summary>
    [JsonProperty("seeders")]
    public int SeedsNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of leechers.
    /// </summary>
    [JsonProperty("leechers")]
    public int LeechersNumber { get; set; }

    /// <summary>
    /// Gets or sets the quality.
    /// </summary>
    [JsonProperty("quality")]
    public RadarrQuality? Quality { get; set; }

    /// <summary>
    /// Gets or sets the publish date.
    /// </summary>
    [JsonProperty("publishDate")]
    public DateTime? PublishDate { get; set; }
}

/// <summary>
/// Represents a Radarr quality.
/// </summary>
public class RadarrQuality
{
    /// <summary>
    /// Gets or sets the quality name.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
} 