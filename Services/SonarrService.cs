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
    /// Searches for TV show torrents using Sonarr.
    /// </summary>
    /// <param name="title">The TV show title.</param>
    /// <param name="year">The release year.</param>
    /// <param name="season">The season number.</param>
    /// <param name="quality">The preferred quality.</param>
    /// <returns>A collection of torrent results.</returns>
    public async Task<IEnumerable<TorrentResult>> SearchTVShowTorrentsAsync(
        string title, 
        int? year = null, 
        int? season = null, 
        string quality = "1080p")
    {
        try
        {
            if (_sonarrConfig == null || !_sonarrConfig.Enabled)
            {
                _logger.LogWarning("Sonarr is not configured or disabled");
                return Enumerable.Empty<TorrentResult>();
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                _logger.LogWarning("Title is required for Sonarr search");
                return Enumerable.Empty<TorrentResult>();
            }

            _logger.LogInformation("Searching Sonarr for TV show: {Title}", title);

            // First, search for the series
            var seriesId = await FindSeriesIdAsync(title, year);
            if (seriesId == null)
            {
                _logger.LogWarning("Could not find series ID for: {Title}", title);
                return Enumerable.Empty<TorrentResult>();
            }

            // Search for releases
            var releases = await SearchReleasesAsync(seriesId.Value, season, quality);
            
            return releases.Select(ConvertToTorrentResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Sonarr for TV show: {Title}", title);
            return Enumerable.Empty<TorrentResult>();
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
                    // Find the best match
                    var bestMatch = series.FirstOrDefault(s => 
                        s.Title.Equals(title, StringComparison.OrdinalIgnoreCase) ||
                        (year.HasValue && s.Year == year.Value));

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
/// Represents a Sonarr series.
/// </summary>
public class SonarrSeries
{
    /// <summary>
    /// Gets or sets the series ID.
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
    /// Gets or sets the TVDB ID.
    /// </summary>
    [JsonProperty("tvdbId")]
    public int? TvdbId { get; set; }
}

/// <summary>
/// Represents a Sonarr episode.
/// </summary>
public class SonarrEpisode
{
    /// <summary>
    /// Gets or sets the episode ID.
    /// </summary>
    [JsonProperty("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonProperty("seasonNumber")]
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    [JsonProperty("episodeNumber")]
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
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