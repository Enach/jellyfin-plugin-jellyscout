using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Jellyfin.Plugin.JellyScout.Models;

namespace Jellyfin.Plugin.JellyScout.Services
{
    public class TMDBService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TMDBService> _logger;
        private const string BaseUrl = "https://api.themoviedb.org/3";

        public TMDBService(ILogger<TMDBService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public async Task<List<TmdbMovie>> SearchMoviesAsync(string query, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = Jellyfin.Plugin.JellyScout.Plugin.Instance?.Configuration;
                if (string.IsNullOrEmpty(config?.TmdbApiKey))
                {
                    _logger.LogWarning("TMDB API key not configured");
                    return new List<TmdbMovie>();
                }

                var url = $"{BaseUrl}/search/movie?api_key={config.TmdbApiKey}&query={Uri.EscapeDataString(query)}&language={config.Language}&region={config.Region}&include_adult={config.IncludeAdult.ToString().ToLower()}";
                
                var response = await _httpClient.GetStringAsync(url, cancellationToken);
                var searchResult = JsonConvert.DeserializeObject<TmdbSearchResponse<TmdbMovie>>(response);

                if (searchResult?.Results == null)
                {
                    return new List<TmdbMovie>();
                }

                return searchResult.Results.Take(config.MaxSearchResults).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for movies with query: {Query}", query);
                return new List<TmdbMovie>();
            }
        }

        public async Task<List<TmdbTvShow>> SearchTvShowsAsync(string query, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = Jellyfin.Plugin.JellyScout.Plugin.Instance?.Configuration;
                if (string.IsNullOrEmpty(config?.TmdbApiKey))
                {
                    _logger.LogWarning("TMDB API key not configured");
                    return new List<TmdbTvShow>();
                }

                var url = $"{BaseUrl}/search/tv?api_key={config.TmdbApiKey}&query={Uri.EscapeDataString(query)}&language={config.Language}&region={config.Region}&include_adult={config.IncludeAdult.ToString().ToLower()}";
                
                var response = await _httpClient.GetStringAsync(url, cancellationToken);
                var searchResult = JsonConvert.DeserializeObject<TmdbSearchResponse<TmdbTvShow>>(response);

                if (searchResult?.Results == null)
                {
                    return new List<TmdbTvShow>();
                }

                return searchResult.Results.Take(config.MaxSearchResults).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for TV shows with query: {Query}", query);
                return new List<TmdbTvShow>();
            }
        }

        public async Task<List<TmdbSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = Jellyfin.Plugin.JellyScout.Plugin.Instance?.Configuration;
                if (string.IsNullOrEmpty(config?.TmdbApiKey))
                {
                    _logger.LogWarning("TMDB API key not configured");
                    return new List<TmdbSearchResult>();
                }

                var url = $"{BaseUrl}/search/multi?api_key={config.TmdbApiKey}&query={Uri.EscapeDataString(query)}&language={config.Language}&region={config.Region}&include_adult={config.IncludeAdult.ToString().ToLower()}";
                
                var response = await _httpClient.GetStringAsync(url, cancellationToken);
                var searchResult = JsonConvert.DeserializeObject<TmdbSearchResponse<TmdbSearchResult>>(response);

                if (searchResult?.Results == null)
                {
                    return new List<TmdbSearchResult>();
                }

                return searchResult.Results.Take(config.MaxSearchResults).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching with query: {Query}", query);
                return new List<TmdbSearchResult>();
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 