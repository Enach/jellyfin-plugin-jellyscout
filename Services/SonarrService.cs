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
/// Service for integrating with Sonarr for TV show torrent searching.
/// </summary>
public class SonarrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SonarrService> _logger;
    private SonarrConfiguration? _sonarrConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="SonarrService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="logger">The logger.</param>
    public SonarrService(HttpClient httpClient, ILogger<SonarrService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sets the Sonarr configuration.
    /// </summary>
    /// <param name="config">The Sonarr configuration.</param>
    public void SetSonarrConfiguration(SonarrConfiguration config)
    {
        _sonarrConfig = config;
        _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    }

    /// <summary>
    /// Adds a TV show to Sonarr and searches for it.
    /// </summary>
    /// <param name="title">The TV show title.</param>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <param name="year">The release year.</param>
    /// <param name="qualityProfileId">The quality profile ID.</param>
    /// <returns>True if successfully added.</returns>
    public async Task<bool> AddTVShowAsync(string title, int tmdbId, int? year = null, int? qualityProfileId = null)
    {
        try
        {
            if (_sonarrConfig == null || !_sonarrConfig.Enabled)
            {
                _logger.LogWarning("Sonarr is not configured or disabled");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_sonarrConfig.ApiKey))
            {
                _logger.LogError("Sonarr API key is not configured");
                return false;
            }

            _logger.LogInformation("Adding TV show to Sonarr: {Title} (TMDB: {TmdbId})", title, tmdbId);

            // First, lookup the series using Sonarr's lookup API
            var lookupResults = await LookupSeriesAsync(title, year);
            if (!lookupResults.Any())
            {
                _logger.LogError("Could not find series in Sonarr lookup: {Title}", title);
                return false;
            }

            var selectedSeries = lookupResults.First();

            // Check if series already exists in Sonarr
            var existingSeries = await GetExistingSeriesAsync(selectedSeries.TvdbId);
            if (existingSeries != null)
            {
                _logger.LogInformation("Series already exists in Sonarr: {Title}", title);
                return true; // Already exists, consider it successful
            }

            // Get root folders
            var rootFolders = await GetRootFoldersAsync();
            var rootFolderPath = rootFolders.FirstOrDefault()?.Path ?? "/tv/";

            // Add series to Sonarr using the lookup result
            var addSeriesRequest = new
            {
                title = selectedSeries.Title,
                titleSlug = selectedSeries.TitleSlug,
                tvdbId = selectedSeries.TvdbId,
                qualityProfileId = qualityProfileId ?? _sonarrConfig.QualityProfileId,
                languageProfileId = 1, // Default language profile
                rootFolderPath = rootFolderPath,
                monitored = true,
                seasonFolder = true,
                addOptions = new
                {
                    searchForMissingEpisodes = true,
                    ignoreEpisodesWithFiles = false,
                    ignoreEpisodesWithoutFiles = false
                }
            };

            var jsonContent = JsonConvert.SerializeObject(addSeriesRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.PostAsync($"{_sonarrConfig.ServerUrl}/api/v3/series", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully added TV show to Sonarr: {Title}", title);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Sonarr API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding TV show to Sonarr: {Title}", title);
            return false;
        }
    }

    /// <summary>
    /// Gets the download status of a TV show by TMDB ID.
    /// </summary>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <returns>Download status information.</returns>
    public async Task<DownloadStatus> GetDownloadStatusAsync(int tmdbId)
    {
        try
        {
            if (_sonarrConfig == null || !_sonarrConfig.Enabled)
            {
                return new DownloadStatus { Status = MediaStatus.NotInSystem, Message = "Sonarr not configured" };
            }

            // First check if series exists in Sonarr
            var series = await GetExistingSeriesByTmdbIdAsync(tmdbId);
            if (series == null)
            {
                return new DownloadStatus { Status = MediaStatus.NotInSystem, Message = "Not added to Sonarr" };
            }

            // Check download queue for active downloads
            var queueItems = await GetQueueAsync();
            var activeDownloads = queueItems.Where(q => q.SeriesId == series.Id).ToList();

            if (activeDownloads.Any())
            {
                var downloadProgress = activeDownloads.Average(d => d.Size > 0 ? (double)d.SizeLeft / d.Size * 100 : 0);
                return new DownloadStatus 
                { 
                    Status = MediaStatus.Downloading, 
                    Message = $"Downloading ({activeDownloads.Count} items)",
                    Progress = (int)(100 - downloadProgress),
                    Details = activeDownloads.Select(d => d.Title).ToList()
                };
            }

            // Check if all episodes are downloaded
            var episodes = await GetSeriesEpisodesAsync(series.Id);
            var totalEpisodes = episodes.Count();
            var downloadedEpisodes = episodes.Count(e => e.HasFile);

            if (downloadedEpisodes == totalEpisodes && totalEpisodes > 0)
            {
                return new DownloadStatus 
                { 
                    Status = MediaStatus.Downloaded, 
                    Message = $"Complete ({downloadedEpisodes}/{totalEpisodes} episodes)",
                    Progress = 100
                };
            }
            else if (downloadedEpisodes > 0)
            {
                return new DownloadStatus 
                { 
                    Status = MediaStatus.PartiallyDownloaded, 
                    Message = $"Partial ({downloadedEpisodes}/{totalEpisodes} episodes)",
                    Progress = totalEpisodes > 0 ? (int)((double)downloadedEpisodes / totalEpisodes * 100) : 0
                };
            }
            else
            {
                return new DownloadStatus 
                { 
                    Status = MediaStatus.Wanted, 
                    Message = "Added but not downloaded",
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
    /// Gets the current download queue from Sonarr.
    /// </summary>
    /// <returns>List of queue items.</returns>
    private async Task<IEnumerable<SonarrQueueItem>> GetQueueAsync()
    {
        if (_sonarrConfig == null)
        {
            return Enumerable.Empty<SonarrQueueItem>();
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.GetAsync($"{_sonarrConfig.ServerUrl}/api/v3/queue");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var queueResponse = JsonConvert.DeserializeObject<SonarrQueueResponse>(content);
                return queueResponse?.Records ?? Enumerable.Empty<SonarrQueueItem>();
            }

            return Enumerable.Empty<SonarrQueueItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Sonarr queue");
            return Enumerable.Empty<SonarrQueueItem>();
        }
    }

    /// <summary>
    /// Gets episodes for a series.
    /// </summary>
    /// <param name="seriesId">The series ID.</param>
    /// <returns>List of episodes.</returns>
    private async Task<IEnumerable<SonarrEpisode>> GetSeriesEpisodesAsync(int seriesId)
    {
        if (_sonarrConfig == null)
        {
            return Enumerable.Empty<SonarrEpisode>();
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.GetAsync($"{_sonarrConfig.ServerUrl}/api/v3/episode?seriesId={seriesId}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var episodes = JsonConvert.DeserializeObject<SonarrEpisode[]>(content);
                return episodes ?? Enumerable.Empty<SonarrEpisode>();
            }

            return Enumerable.Empty<SonarrEpisode>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting episodes for series ID: {SeriesId}", seriesId);
            return Enumerable.Empty<SonarrEpisode>();
        }
    }

    /// <summary>
    /// Gets existing series by TMDB ID.
    /// </summary>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <returns>The series if found.</returns>
    private async Task<SonarrSeries?> GetExistingSeriesByTmdbIdAsync(int tmdbId)
    {
        if (_sonarrConfig == null)
        {
            return null;
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.GetAsync($"{_sonarrConfig.ServerUrl}/api/v3/series");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var allSeries = JsonConvert.DeserializeObject<SonarrSeries[]>(content);
                
                // Find series with matching TMDB ID
                return allSeries?.FirstOrDefault(s => s.TmdbId == tmdbId);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting existing series by TMDB ID: {TmdbId}", tmdbId);
            return null;
        }
    }

    /// <summary>
    /// Searches for a specific episode.
    /// </summary>
    /// <param name="title">The TV show title.</param>
    /// <param name="season">The season number.</param>
    /// <param name="episode">The episode number.</param>
    /// <param name="quality">The preferred quality.</param>
    /// <returns>A collection of torrent results.</returns>
    public async Task<IEnumerable<TorrentResult>> SearchEpisodeAsync(
        string title, 
        int season, 
        int episode, 
        string quality = "1080p")
    {
        try
        {
            if (_sonarrConfig == null || !_sonarrConfig.Enabled)
            {
                _logger.LogWarning("Sonarr is not configured or disabled");
                return Enumerable.Empty<TorrentResult>();
            }

            _logger.LogInformation("Searching Sonarr for episode: {Title} S{Season:D2}E{Episode:D2}", title, season, episode);

            // Search for the specific episode
            var episodeSearchRequest = new
            {
                name = "EpisodeSearch",
                seriesId = await FindSeriesIdAsync(title),
                episodeIds = new[] { await FindEpisodeIdAsync(title, season, episode) }
            };

            var jsonContent = JsonConvert.SerializeObject(episodeSearchRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_sonarrConfig.ServerUrl}/api/v3/command", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Episode search command sent successfully");
                // In a real implementation, you would wait for the command to complete
                // and then fetch the results
                return await GetRecentReleasesAsync(title, season, episode);
            }
            else
            {
                _logger.LogError("Sonarr API error: {StatusCode} - {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            }

            return Enumerable.Empty<TorrentResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Sonarr for episode: {Title} S{Season:D2}E{Episode:D2}", title, season, episode);
            return Enumerable.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Searches for series using Sonarr's lookup API.
    /// </summary>
    /// <param name="title">The series title.</param>
    /// <param name="year">The release year.</param>
    /// <returns>Collection of series lookup results.</returns>
    private async Task<IEnumerable<SonarrSeries>> LookupSeriesAsync(string title, int? year = null)
    {
        if (_sonarrConfig == null)
        {
            _logger.LogWarning("Sonarr configuration is not set");
            return Enumerable.Empty<SonarrSeries>();
        }

        try
        {
            var searchUrl = $"{_sonarrConfig.ServerUrl}/api/v3/series/lookup?term={Uri.EscapeDataString(title)}";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(searchUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var series = JsonConvert.DeserializeObject<SonarrSeries[]>(content);

                if (series != null && series.Length > 0)
                {
                    // Return all series since TV shows don't have a single "year"
                    return series;
                }
            }

            return Enumerable.Empty<SonarrSeries>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up series: {Title}", title);
            return Enumerable.Empty<SonarrSeries>();
        }
    }

    /// <summary>
    /// Gets existing series by TVDB ID.
    /// </summary>
    /// <param name="tvdbId">The TVDB ID.</param>
    /// <returns>Existing series or null.</returns>
    private async Task<SonarrSeries?> GetExistingSeriesAsync(int? tvdbId)
    {
        if (_sonarrConfig == null)
        {
            _logger.LogWarning("Sonarr configuration is not set");
            return null;
        }

        try
        {
            var seriesUrl = $"{_sonarrConfig.ServerUrl}/api/v3/series";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(seriesUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var series = JsonConvert.DeserializeObject<SonarrSeries[]>(content);

                return series?.FirstOrDefault(s => s.TvdbId.HasValue && s.TvdbId == tvdbId);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting existing series with TVDB ID: {TvdbId}", tvdbId);
            return null;
        }
    }

    /// <summary>
    /// Gets root folders from Sonarr.
    /// </summary>
    /// <returns>Collection of root folders.</returns>
    private async Task<IEnumerable<SonarrRootFolder>> GetRootFoldersAsync()
    {
        if (_sonarrConfig == null)
        {
            _logger.LogWarning("Sonarr configuration is not set");
            return Enumerable.Empty<SonarrRootFolder>();
        }

        try
        {
            var rootFolderUrl = $"{_sonarrConfig.ServerUrl}/api/v3/rootfolder";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(rootFolderUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var rootFolders = JsonConvert.DeserializeObject<SonarrRootFolder[]>(content);

                return rootFolders ?? Enumerable.Empty<SonarrRootFolder>();
            }

            return Enumerable.Empty<SonarrRootFolder>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting root folders from Sonarr");
            return Enumerable.Empty<SonarrRootFolder>();
        }
    }

    private async Task<int?> FindSeriesIdAsync(string title, int? year = null)
    {
        if (_sonarrConfig == null)
        {
            _logger.LogWarning("Sonarr configuration is not set");
            return null;
        }

        try
        {
            var searchUrl = $"{_sonarrConfig.ServerUrl}/api/v3/series/lookup?term={Uri.EscapeDataString(title)}";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(searchUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var series = JsonConvert.DeserializeObject<SonarrSeries[]>(content);

                if (series != null && series.Length > 0)
                {
                    // Find the best match by title
                    var bestMatch = series.FirstOrDefault(s => 
                        s.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

                    return bestMatch?.Id ?? series[0].Id;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding series ID for: {Title}", title);
            return null;
        }
    }

    private async Task<int?> FindEpisodeIdAsync(string title, int season, int episode)
    {
        if (_sonarrConfig == null)
        {
            _logger.LogWarning("Sonarr configuration is not set");
            return null;
        }

        var seriesId = await FindSeriesIdAsync(title);
        if (seriesId == null)
            return null;

        try
        {
            var episodeUrl = $"{_sonarrConfig.ServerUrl}/api/v3/episode?seriesId={seriesId}";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(episodeUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var episodes = JsonConvert.DeserializeObject<SonarrEpisode[]>(content);

                var targetEpisode = episodes?.FirstOrDefault(e => 
                    e.SeasonNumber == season && e.EpisodeNumber == episode);

                return targetEpisode?.Id;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding episode ID for: {Title} S{Season:D2}E{Episode:D2}", title, season, episode);
            return null;
        }
    }

    private async Task<IEnumerable<SonarrRelease>> SearchReleasesAsync(int seriesId, int? season = null, string quality = "1080p")
    {
        if (_sonarrConfig == null)
        {
            _logger.LogWarning("Sonarr configuration is not set");
            return Enumerable.Empty<SonarrRelease>();
        }

        try
        {
            var releaseUrl = $"{_sonarrConfig.ServerUrl}/api/v3/release?seriesId={seriesId}";
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _sonarrConfig.ApiKey);

            var response = await _httpClient.GetAsync(releaseUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var releases = JsonConvert.DeserializeObject<SonarrRelease[]>(content);

                if (releases != null)
                {
                    // Filter by season if specified
                    var filteredReleases = releases.AsEnumerable();
                    
                    if (season.HasValue)
                    {
                        filteredReleases = filteredReleases.Where(r => 
                            r.Title.Contains($"S{season:D2}", StringComparison.OrdinalIgnoreCase));
                    }

                    // Filter by quality
                    if (!string.IsNullOrEmpty(quality))
                    {
                        filteredReleases = filteredReleases.Where(r => 
                            r.Title.Contains(quality, StringComparison.OrdinalIgnoreCase));
                    }

                    return filteredReleases.OrderByDescending(r => r.SeedsNumber).Take(10);
                }
            }

            return Enumerable.Empty<SonarrRelease>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching releases for series ID: {SeriesId}", seriesId);
            return Enumerable.Empty<SonarrRelease>();
        }
    }

    private async Task<IEnumerable<TorrentResult>> GetRecentReleasesAsync(string title, int season, int episode)
    {
        // This is a placeholder for getting recent releases after a search command
        // In a real implementation, you would poll the Sonarr API for command completion
        // and then fetch the available releases
        await Task.Delay(1000);
        
        var mockResults = new List<TorrentResult>
        {
            new TorrentResult
            {
                Name = $"{title} S{season:D2}E{episode:D2} 1080p WEB-DL",
                Size = "1.5 GB",
                Seeders = 50,
                Leechers = 5,
                Quality = "1080p",
                Provider = "Sonarr",
                MagnetLink = "magnet:?xt=urn:btih:example"
            }
        };

        return mockResults;
    }

    private static TorrentResult ConvertToTorrentResult(SonarrRelease release)
    {
        return new TorrentResult
        {
            Name = release.Title,
            Size = FormatFileSize(release.Size),
            Seeders = release.SeedsNumber,
            Leechers = release.LeechersNumber,
            Quality = ExtractQuality(release.Title),
            Provider = "Sonarr",
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
}

/// <summary>
/// Represents a Sonarr release.
/// </summary>
public class SonarrRelease
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
    public SonarrQuality? Quality { get; set; }

    /// <summary>
    /// Gets or sets the publish date.
    /// </summary>
    [JsonProperty("publishDate")]
    public DateTime? PublishDate { get; set; }
}

/// <summary>
/// Represents a Sonarr quality.
/// </summary>
public class SonarrQuality
{
    /// <summary>
    /// Gets or sets the quality name.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
} 