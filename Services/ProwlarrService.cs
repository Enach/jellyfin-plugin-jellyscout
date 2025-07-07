using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.JellyScout.Configuration;
using JellyScout.Models;

namespace JellyScout.Services
{
    /// <summary>
    /// Service for interacting with Prowlarr API to search for torrents.
    /// </summary>
    public class ProwlarrService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProwlarrService> _logger;
        private ProwlarrConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProwlarrService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="config">The Prowlarr configuration.</param>
        public ProwlarrService(HttpClient httpClient, ILogger<ProwlarrService> logger, ProwlarrConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Searches for torrents using Prowlarr.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="category">The category filter (optional).</param>
        /// <returns>A list of torrent search results.</returns>
        public async Task<List<TorrentSearchResult>> SearchTorrentsAsync(string query, string category = null)
        {
            if (!_config.Enabled || string.IsNullOrEmpty(_config.ServerUrl) || string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogWarning("Prowlarr is not properly configured");
                return new List<TorrentSearchResult>();
            }

            try
            {
                var searchUrl = $"{_config.ServerUrl.TrimEnd('/')}/api/v1/search";
                var parameters = new List<string>
                {
                    $"query={Uri.EscapeDataString(query)}",
                    $"apikey={_config.ApiKey}"
                };

                if (!string.IsNullOrEmpty(category))
                {
                    parameters.Add($"cat={category}");
                }

                var fullUrl = $"{searchUrl}?{string.Join("&", parameters)}";
                _logger.LogInformation("Searching Prowlarr: {Query}", query);

                var response = await _httpClient.GetAsync(fullUrl);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var searchResults = JsonSerializer.Deserialize<List<ProwlarrSearchResult>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return searchResults?.Select(MapToTorrentSearchResult).ToList() ?? new List<TorrentSearchResult>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching torrents via Prowlarr");
                return new List<TorrentSearchResult>();
            }
        }

        /// <summary>
        /// Gets the best torrent for streaming based on seeder count and quality.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="mediaType">The media type (movie/tv).</param>
        /// <returns>The best torrent for streaming, or null if none suitable.</returns>
        public async Task<TorrentSearchResult> GetBestTorrentForStreamingAsync(string query, string mediaType = "movie")
        {
            var category = mediaType.ToLower() == "tv" ? "5000" : "2000"; // TV or Movies
            var results = await SearchTorrentsAsync(query, category);

            if (!results.Any())
            {
                return null;
            }

            // Filter by minimum seeders
            var viableTorrents = results.Where(t => t.Seeders >= _config.MinSeeders).ToList();

            if (!viableTorrents.Any())
            {
                _logger.LogInformation("No torrents found with minimum {MinSeeders} seeders for: {Query}", _config.MinSeeders, query);
                return null;
            }

            // Sort by seeders (descending) then by size (prefer reasonable sizes)
            var bestTorrent = viableTorrents
                .OrderByDescending(t => t.Seeders)
                .ThenBy(t => Math.Abs(t.Size - 2000000000L)) // Prefer ~2GB files
                .FirstOrDefault();

            _logger.LogInformation("Found suitable torrent: {Title} ({Seeders} seeders, {Size:F1} GB)", 
                bestTorrent?.Title, bestTorrent?.Seeders, bestTorrent?.Size / 1024.0 / 1024.0 / 1024.0);

            return bestTorrent;
        }

        /// <summary>
        /// Updates the configuration.
        /// </summary>
        /// <param name="config">The new configuration.</param>
        public void UpdateConfiguration(ProwlarrConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Maps a Prowlarr search result to our torrent search result model.
        /// </summary>
        /// <param name="prowlarrResult">The Prowlarr result.</param>
        /// <returns>The mapped torrent search result.</returns>
        private TorrentSearchResult MapToTorrentSearchResult(ProwlarrSearchResult prowlarrResult)
        {
            return new TorrentSearchResult
            {
                Title = prowlarrResult.Title,
                DownloadUrl = prowlarrResult.DownloadUrl,
                MagnetUrl = prowlarrResult.MagnetUrl,
                Size = prowlarrResult.Size,
                Seeders = prowlarrResult.Seeders,
                Leechers = prowlarrResult.Leechers,
                Quality = ExtractQuality(prowlarrResult.Title),
                Provider = prowlarrResult.Tracker,
                PublishDate = prowlarrResult.PublishDate
            };
        }

        /// <summary>
        /// Extracts quality information from the torrent title.
        /// </summary>
        /// <param name="title">The torrent title.</param>
        /// <returns>The extracted quality.</returns>
        private string ExtractQuality(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "Unknown";

            var upperTitle = title.ToUpper();
            
            if (upperTitle.Contains("2160P") || upperTitle.Contains("4K"))
                return "4K";
            if (upperTitle.Contains("1080P"))
                return "1080p";
            if (upperTitle.Contains("720P"))
                return "720p";
            if (upperTitle.Contains("480P"))
                return "480p";
            
            return "Unknown";
        }
    }

    /// <summary>
    /// Represents a search result from Prowlarr.
    /// </summary>
    public class ProwlarrSearchResult
    {
        public string Title { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string MagnetUrl { get; set; } = string.Empty;
        public long Size { get; set; }
        public int Seeders { get; set; }
        public int Leechers { get; set; }
        public string Tracker { get; set; } = string.Empty;
        public DateTime PublishDate { get; set; }
    }
} 