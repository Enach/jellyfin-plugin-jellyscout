using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyScout.Configuration;
using Jellyfin.Plugin.JellyScout.Models;
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
    /// Adds a movie to Radarr and searches for it.
    /// </summary>
    /// <param name="title">The movie title.</param>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <param name="year">The release year.</param>
    /// <param name="qualityProfileId">The quality profile ID.</param>
    /// <returns>True if successfully added.</returns>
    public async Task<bool> AddMovieAsync(string title, int tmdbId, int? year = null, int? qualityProfileId = null)
    {
        try
        {
            if (_radarrConfig == null || !_radarrConfig.Enabled)
            {
                _logger.LogWarning("Radarr is not configured or disabled");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_radarrConfig.ApiKey))
            {
                _logger.LogError("Radarr API key is not configured");
                return false;
            }

            _logger.LogInformation("Adding movie to Radarr: {Title} (TMDB: {TmdbId})", title, tmdbId);

            // First, lookup the movie using Radarr's lookup API
            var lookupResults = await LookupMovieAsync(title, year, tmdbId);
            if (!lookupResults.Any())
            {
                _logger.LogError("Could not find movie in Radarr lookup: {Title}", title);
                return false;
            }

            var selectedMovie = lookupResults.First();

            // Check if movie already exists in Radarr
            var existingMovie = await GetExistingMovieAsync(selectedMovie.TmdbId);
            if (existingMovie != null)
            {
                _logger.LogInformation("Movie already exists in Radarr: {Title}", title);
                return true; // Already exists, consider it successful
            }

            // Get root folders
            var rootFolders = await GetRootFoldersAsync();
            var rootFolderPath = rootFolders.FirstOrDefault()?.Path ?? "/movies/";

            // Add movie to Radarr using the lookup result
            var addMovieRequest = new
            {
                title = selectedMovie.Title,
                titleSlug = selectedMovie.TitleSlug,
                tmdbId = selectedMovie.TmdbId,
                year = selectedMovie.Year,
                qualityProfileId = qualityProfileId ?? _radarrConfig.QualityProfileId,
                rootFolderPath = rootFolderPath,
                monitored = true,
                minimumAvailability = "announced", // or "inCinemas", "released", "preDB"
                addOptions = new
                {
                    searchForMovie = true,
                    ignoreEpisodesWithFiles = false,
                    ignoreEpisodesWithoutFiles = false
                }
            };

            var jsonContent = JsonConvert.SerializeObject(addMovieRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _radarrConfig.ApiKey);

            var response = await _httpClient.PostAsync($"{_radarrConfig.ServerUrl}/api/v3/movie", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully added movie to Radarr: {Title}", title);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Radarr API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding movie to Radarr: {Title}", title);
            return false;
        }
    }

    /// <summary>
    /// Gets the download status of a movie by TMDB ID.
    /// </summary>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <returns>Download status information.</returns>
    public async Task<DownloadStatus> GetDownloadStatusAsync(int tmdbId)
    {
        try
        {
            if (_radarrConfig == null || !_radarrConfig.Enabled)
            {
                return new DownloadStatus { Status = MediaStatus.NotInSystem, Message = "Radarr not configured" };
            }

            // First check if movie exists in Radarr
            var movie = await GetExistingMovieByTmdbIdAsync(tmdbId);
            if (movie == null)
            {
                return new DownloadStatus { Status = MediaStatus.NotInSystem, Message = "Not added to Radarr" };
            }

            // Check download queue for active downloads
            var queueItems = await GetQueueAsync();
            var activeDownload = queueItems.FirstOrDefault(q => q.MovieId == movie.Id);

            if (activeDownload != null)
            {
                var downloadProgress = activeDownload.Size > 0 ? (double)activeDownload.SizeLeft / activeDownload.Size * 100 : 0;
                return new DownloadStatus 
                { 
                    Status = MediaStatus.Downloading, 
                    Message = $"Downloading ({activeDownload.Title})",
                    Progress = (int)(100 - downloadProgress),
                    Details = new List<string> { activeDownload.Title }
                };
            }

            // Check if movie is downloaded
            if (movie.HasFile)
            {
                return new DownloadStatus 
                { 
                    Status = MediaStatus.Downloaded, 
                    Message = "Downloaded",
                    Progress = 100
                };
            }
            else if (movie.Monitored)
            {
                return new DownloadStatus 
                { 
                    Status = MediaStatus.Wanted, 
                    Message = "Added but not downloaded",
                    Progress = 0
                };
            }
            else
            {
                return new DownloadStatus 
                { 
                    Status = MediaStatus.NotMonitored, 
                    Message = "Added but not monitored",
                    Progress = 0
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting download status for TMDB ID: {TmdbId}", tmdbId);
            return new DownloadStatus { Status = MediaStatus.Failed, Message = $"Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Gets the current download queue from Radarr.
    /// </summary>
    /// <returns>List of queue items.</returns>
    private async Task<IEnumerable<RadarrQueueItem>> GetQueueAsync()
    {
        if (_radarrConfig == null)
        {
            return Enumerable.Empty<RadarrQueueItem>();
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _radarrConfig.ApiKey);

            var response = await _httpClient.GetAsync($"{_radarrConfig.ServerUrl}/api/v3/queue");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var queueResponse = JsonConvert.DeserializeObject<RadarrQueueResponse>(content);
                return queueResponse?.Records ?? Enumerable.Empty<RadarrQueueItem>();
            }

            return Enumerable.Empty<RadarrQueueItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Radarr queue");
            return Enumerable.Empty<RadarrQueueItem>();
        }
    }

    /// <summary>
    /// Gets existing movie by TMDB ID.
    /// </summary>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <returns>The movie if found.</returns>
    private async Task<RadarrMovie?> GetExistingMovieByTmdbIdAsync(int tmdbId)
    {
        if (_radarrConfig == null)
        {
            return null;
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _radarrConfig.ApiKey);

            var response = await _httpClient.GetAsync($"{_radarrConfig.ServerUrl}/api/v3/movie");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var allMovies = JsonConvert.DeserializeObject<RadarrMovie[]>(content);
                
                // Find movie with matching TMDB ID
                return allMovies?.FirstOrDefault(m => m.TmdbId == tmdbId);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting existing movie by TMDB ID: {TmdbId}", tmdbId);
            return null;
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

    /// <summary>
    /// Searches for movies using Radarr's lookup API.
    /// </summary>
    /// <param name="title">The movie title.</param>
    /// <param name="year">The release year.</param>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <returns>Collection of movie lookup results.</returns>
    private async Task<IEnumerable<RadarrMovie>> LookupMovieAsync(string title, int? year = null, int? tmdbId = null)
    {
        if (_radarrConfig == null)
        {
            _logger.LogWarning("Radarr configuration is not set");
            return Enumerable.Empty<RadarrMovie>();
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
                    // Filter by TMDB ID if provided
                    if (tmdbId.HasValue)
                    {
                        var tmdbMatches = movies.Where(m => m.TmdbId == tmdbId.Value).ToArray();
                        if (tmdbMatches.Length > 0)
                        {
                            return tmdbMatches;
                        }
                    }
                    
                    // Filter by year if provided
                    if (year.HasValue)
                    {
                        var yearMatches = movies.Where(m => m.Year == year.Value).ToArray();
                        if (yearMatches.Length > 0)
                        {
                            return yearMatches;
                        }
                    }
                    
                    return movies;
                }
            }

            return Enumerable.Empty<RadarrMovie>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up movie: {Title}", title);
            return Enumerable.Empty<RadarrMovie>();
        }
    }

    /// <summary>
    /// Gets existing movie by TMDB ID.
    /// </summary>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <returns>Existing movie or null.</returns>
    private async Task<RadarrMovie?> GetExistingMovieAsync(int tmdbId)
    {
        if (_radarrConfig == null)
        {
            _logger.LogWarning("Radarr configuration is not set");
            return null;
        }

        try
        {
            var moviesUrl = $"{_radarrConfig.ServerUrl}/api/v3/movie";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _radarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(moviesUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var movies = JsonConvert.DeserializeObject<RadarrMovie[]>(content);

                return movies?.FirstOrDefault(m => m.TmdbId == tmdbId);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting existing movie with TMDB ID: {TmdbId}", tmdbId);
            return null;
        }
    }

    /// <summary>
    /// Gets root folders from Radarr.
    /// </summary>
    /// <returns>Collection of root folders.</returns>
    private async Task<IEnumerable<RadarrRootFolder>> GetRootFoldersAsync()
    {
        if (_radarrConfig == null)
        {
            _logger.LogWarning("Radarr configuration is not set");
            return Enumerable.Empty<RadarrRootFolder>();
        }

        try
        {
            var rootFolderUrl = $"{_radarrConfig.ServerUrl}/api/v3/rootfolder";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _radarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(rootFolderUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var rootFolders = JsonConvert.DeserializeObject<RadarrRootFolder[]>(content);

                return rootFolders ?? Enumerable.Empty<RadarrRootFolder>();
            }

            return Enumerable.Empty<RadarrRootFolder>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting root folders from Radarr");
            return Enumerable.Empty<RadarrRootFolder>();
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