using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.JellyScout.Configuration;
using JellyScout.Models;

namespace JellyScout.Services
{
    /// <summary>
    /// Service for interacting with BitPlay streaming server.
    /// </summary>
    public class BitPlayService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BitPlayService> _logger;
        private BitPlayConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitPlayService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="config">The BitPlay configuration.</param>
        public BitPlayService(HttpClient httpClient, ILogger<BitPlayService> logger, BitPlayConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Starts streaming from a Prowlarr download URL.
        /// </summary>
        /// <param name="downloadUrl">The Prowlarr download URL.</param>
        /// <param name="fileName">The original filename for reference.</param>
        /// <returns>The streaming information.</returns>
        public async Task<StreamingInfo> StartStreamingFromUrlAsync(string downloadUrl, string fileName)
        {
            if (!_config.Enabled || string.IsNullOrEmpty(_config.ServerUrl))
            {
                _logger.LogWarning("BitPlay is not properly configured");
                return new StreamingInfo { Success = false, Error = "BitPlay not configured" };
            }

            try
            {
                _logger.LogInformation("Starting BitPlay stream for: {FileName}", fileName);

                // BitPlay expects the download URL to be posted to start streaming
                var request = new BitPlayStreamRequest
                {
                    Url = downloadUrl,
                    FileName = fileName
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_config.ServerUrl.TrimEnd('/')}/api/stream", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var streamResponse = JsonSerializer.Deserialize<BitPlayStreamResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (streamResponse != null && !string.IsNullOrEmpty(streamResponse.StreamUrl))
                    {
                        _logger.LogInformation("BitPlay stream started successfully: {StreamUrl}", streamResponse.StreamUrl);
                        return new StreamingInfo
                        {
                            Success = true,
                            StreamUrl = streamResponse.StreamUrl,
                            PlayerUrl = $"{_config.ServerUrl.TrimEnd('/')}/player/{streamResponse.StreamId}",
                            StreamId = streamResponse.StreamId
                        };
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("BitPlay stream failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return new StreamingInfo { Success = false, Error = $"BitPlay error: {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting BitPlay stream");
                return new StreamingInfo { Success = false, Error = "Failed to start stream" };
            }
        }

        /// <summary>
        /// Starts streaming from a magnet URL.
        /// </summary>
        /// <param name="magnetUrl">The magnet URL.</param>
        /// <param name="fileName">The original filename for reference.</param>
        /// <returns>The streaming information.</returns>
        public async Task<StreamingInfo> StartStreamingFromMagnetAsync(string magnetUrl, string fileName)
        {
            if (!_config.Enabled || string.IsNullOrEmpty(_config.ServerUrl))
            {
                _logger.LogWarning("BitPlay is not properly configured");
                return new StreamingInfo { Success = false, Error = "BitPlay not configured" };
            }

            try
            {
                _logger.LogInformation("Starting BitPlay stream from magnet for: {FileName}", fileName);

                var request = new BitPlayMagnetRequest
                {
                    Magnet = magnetUrl,
                    FileName = fileName
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_config.ServerUrl.TrimEnd('/')}/api/magnet", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var streamResponse = JsonSerializer.Deserialize<BitPlayStreamResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (streamResponse != null && !string.IsNullOrEmpty(streamResponse.StreamUrl))
                    {
                        _logger.LogInformation("BitPlay magnet stream started successfully: {StreamUrl}", streamResponse.StreamUrl);
                        return new StreamingInfo
                        {
                            Success = true,
                            StreamUrl = streamResponse.StreamUrl,
                            PlayerUrl = $"{_config.ServerUrl.TrimEnd('/')}/player/{streamResponse.StreamId}",
                            StreamId = streamResponse.StreamId
                        };
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("BitPlay magnet stream failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return new StreamingInfo { Success = false, Error = $"BitPlay error: {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting BitPlay magnet stream");
                return new StreamingInfo { Success = false, Error = "Failed to start stream" };
            }
        }

        /// <summary>
        /// Checks if BitPlay is available and responding.
        /// </summary>
        /// <returns>True if BitPlay is available, false otherwise.</returns>
        public async Task<bool> IsAvailableAsync()
        {
            if (!_config.Enabled || string.IsNullOrEmpty(_config.ServerUrl))
            {
                return false;
            }

            try
            {
                var response = await _httpClient.GetAsync($"{_config.ServerUrl.TrimEnd('/')}/api/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BitPlay health check failed");
                return false;
            }
        }

        /// <summary>
        /// Updates the configuration.
        /// </summary>
        /// <param name="config">The new configuration.</param>
        public void UpdateConfiguration(BitPlayConfiguration config)
        {
            _config = config;
        }
    }

    /// <summary>
    /// Request to start streaming from a download URL.
    /// </summary>
    public class BitPlayStreamRequest
    {
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to start streaming from a magnet URL.
    /// </summary>
    public class BitPlayMagnetRequest
    {
        public string Magnet { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from BitPlay when starting a stream.
    /// </summary>
    public class BitPlayStreamResponse
    {
        public string StreamUrl { get; set; } = string.Empty;
        public string StreamId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Streaming information returned to the client.
    /// </summary>
    public class StreamingInfo
    {
        public bool Success { get; set; }
        public string StreamUrl { get; set; } = string.Empty;
        public string PlayerUrl { get; set; } = string.Empty;
        public string StreamId { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }
} 