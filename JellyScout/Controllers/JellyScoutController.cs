using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.JellyScout.Services;
using Jellyfin.Plugin.JellyScout.Models;

namespace Jellyfin.Plugin.JellyScout.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JellyScoutController : ControllerBase
    {
        private readonly ILogger<JellyScoutController> _logger;
        private readonly TMDBService _tmdbService;

        public JellyScoutController(ILogger<JellyScoutController> logger, TMDBService tmdbService)
        {
            _logger = logger;
            _tmdbService = tmdbService;
        }

        [HttpGet("search/movie")]
        public async Task<ActionResult<List<TmdbMovie>>> SearchMovie([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Search query is required");
                }

                var results = await _tmdbService.SearchMoviesAsync(query);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for movies with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("search/tv")]
        public async Task<ActionResult<List<TmdbTvShow>>> SearchTv([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Search query is required");
                }

                var results = await _tmdbService.SearchTvShowsAsync(query);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for TV shows with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("search/all")]
        public async Task<ActionResult<object>> SearchAll([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Search query is required");
                }

                var movies = await _tmdbService.SearchMoviesAsync(query);
                var tvShows = await _tmdbService.SearchTvShowsAsync(query);

                return Ok(new { movies, tvShows });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for all content with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<TmdbSearchResult>>> Search([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Search query is required");
                }

                var results = await _tmdbService.SearchAsync(query);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching with query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("status")]
        public ActionResult<object> GetStatus()
        {
            try
            {
                var config = Plugin.Instance?.Configuration;
                var hasApiKey = !string.IsNullOrEmpty(config?.TmdbApiKey);

                return Ok(new
                {
                    status = "running",
                    version = "1.0.0",
                    hasApiKey = hasApiKey,
                    channels = new[]
                    {
                        new { id = "tmdb_movies", name = "TMDB Movies", number = "1001" },
                        new { id = "tmdb_tv", name = "TMDB TV Shows", number = "1002" }
                    },
                    configuration = new
                    {
                        maxSearchResults = config?.MaxSearchResults ?? 20,
                        language = config?.Language ?? "en",
                        region = config?.Region ?? "US",
                        includeAdult = config?.IncludeAdult ?? false
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting plugin status");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("channels")]
        public ActionResult<object> GetChannels()
        {
            try
            {
                var channels = new[]
                {
                    new
                    {
                        id = "tmdb_movies",
                        name = "TMDB Movies",
                        number = "1001",
                        callSign = "TMDB-M",
                        type = "TV",
                        description = "Search and discover movies from The Movie Database"
                    },
                    new
                    {
                        id = "tmdb_tv",
                        name = "TMDB TV Shows",
                        number = "1002",
                        callSign = "TMDB-TV",
                        type = "TV",
                        description = "Search and discover TV shows from The Movie Database"
                    }
                };

                return Ok(new { channels });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channels");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("livetv/channels")]
        public ActionResult<object> GetLiveTvChannels()
        {
            try
            {
                // Get channel information from the Live TV service
                var channels = new[]
                {
                    new
                    {
                        number = "2001",
                        name = "BitPlay Streaming",
                        user = "Main Channel",
                        id = "bitplay-main",
                        isMain = true,
                        description = "Default BitPlay streaming channel for all users",
                        url = "/web/index.html#!/livetv.html?channelId=bitplay-main"
                    },
                    new
                    {
                        number = "2156",
                        name = "BitPlay - user1",
                        user = "user1",
                        id = "bitplay-user1",
                        isMain = false,
                        description = "Personalized channel for user1",
                        url = "/web/index.html#!/livetv.html?channelId=bitplay-user1"
                    },
                    new
                    {
                        number = "2387",
                        name = "BitPlay - user2",
                        user = "user2",
                        id = "bitplay-user2",
                        isMain = false,
                        description = "Personalized channel for user2",
                        url = "/web/index.html#!/livetv.html?channelId=bitplay-user2"
                    },
                    new
                    {
                        number = "2642",
                        name = "BitPlay - user3",
                        user = "user3",
                        id = "bitplay-user3",
                        isMain = false,
                        description = "Personalized channel for user3",
                        url = "/web/index.html#!/livetv.html?channelId=bitplay-user3"
                    },
                    new
                    {
                        number = "2234",
                        name = "BitPlay - admin",
                        user = "admin",
                        id = "bitplay-admin",
                        isMain = false,
                        description = "Personalized channel for admin",
                        url = "/web/index.html#!/livetv.html?channelId=bitplay-admin"
                    },
                    new
                    {
                        number = "2891",
                        name = "BitPlay - guest",
                        user = "guest",
                        id = "bitplay-guest",
                        isMain = false,
                        description = "Personalized channel for guest",
                        url = "/web/index.html#!/livetv.html?channelId=bitplay-guest"
                    }
                };

                return Ok(new { channels, totalCount = channels.Length });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Live TV channels");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("livetv/channel/{channelNumber}")]
        public ActionResult RedirectToChannel(string channelNumber)
        {
            try
            {
                // Map channel numbers to channel IDs
                var channelMap = new Dictionary<string, string>
                {
                    { "2001", "bitplay-main" },
                    { "2156", "bitplay-user1" },
                    { "2387", "bitplay-user2" },
                    { "2642", "bitplay-user3" },
                    { "2234", "bitplay-admin" },
                    { "2891", "bitplay-guest" }
                };

                if (channelMap.TryGetValue(channelNumber, out var channelId))
                {
                    var redirectUrl = $"/web/index.html#!/livetv.html?channelId={channelId}";
                    return Redirect(redirectUrl);
                }

                return NotFound($"Channel {channelNumber} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error redirecting to channel {ChannelNumber}", channelNumber);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("livetv/user/{userId}")]
        public ActionResult<object> GetUserChannel(string userId)
        {
            try
            {
                // Generate channel number for user (same logic as in BitPlayLiveTvService)
                var channelNumber = GenerateChannelNumberFromUserId(userId);
                var channelId = $"bitplay-{userId}";

                var channel = new
                {
                    number = channelNumber.ToString(),
                    name = $"BitPlay - {userId}",
                    user = userId,
                    id = channelId,
                    isMain = false,
                    description = $"Personalized channel for {userId}",
                    url = $"/web/index.html#!/livetv.html?channelId={channelId}",
                    directUrl = $"/api/jellyscout/livetv/channel/{channelNumber}"
                };

                return Ok(channel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting channel for user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        private int GenerateChannelNumberFromUserId(string userId)
        {
            // Same logic as in BitPlayLiveTvService
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userId));
                var hashInt = BitConverter.ToInt32(hash, 0);
                var channelNumber = 2100 + (Math.Abs(hashInt) % 900);
                return channelNumber;
            }
        }

        [HttpGet("")]
        public ActionResult SearchInterface()
        {
            try
            {
                var html = GetSearchInterfaceHtml();
                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving search interface");
                return StatusCode(500, "Internal server error");
            }
        }

        private string GetSearchInterfaceHtml()
        {
            var config = Plugin.Instance?.Configuration;
            var hasApiKey = !string.IsNullOrEmpty(config?.TmdbApiKey);
            
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>JellyScout - Movie & TV Discovery</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: #101010;
            color: #ffffff;
            line-height: 1.6;
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }}
        
        .header {{
            text-align: center;
            margin-bottom: 40px;
            padding: 20px 0;
            border-bottom: 2px solid #333;
        }}
        
        .header h1 {{
            font-size: 2.5rem;
            color: #00a4dc;
            margin-bottom: 10px;
        }}
        
        .header p {{
            font-size: 1.1rem;
            color: #ccc;
        }}
        
        .search-section {{
            background: #1a1a1a;
            padding: 30px;
            border-radius: 10px;
            margin-bottom: 30px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
        }}
        
        .search-form {{
            display: flex;
            gap: 15px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }}
        
        .search-input {{
            flex: 1;
            min-width: 300px;
            padding: 12px 16px;
            border: 2px solid #333;
            border-radius: 6px;
            background: #2a2a2a;
            color: #fff;
            font-size: 16px;
        }}
        
        .search-input:focus {{
            outline: none;
            border-color: #00a4dc;
        }}
        
        .search-type {{
            padding: 12px 16px;
            border: 2px solid #333;
            border-radius: 6px;
            background: #2a2a2a;
            color: #fff;
            font-size: 16px;
            cursor: pointer;
        }}
        
        .search-btn {{
            padding: 12px 24px;
            background: #00a4dc;
            color: white;
            border: none;
            border-radius: 6px;
            font-size: 16px;
            cursor: pointer;
            transition: background 0.3s;
        }}
        
        .search-btn:hover {{
            background: #0088b8;
        }}
        
        .search-btn:disabled {{
            background: #666;
            cursor: not-allowed;
        }}
        
        .loading {{
            text-align: center;
            padding: 40px;
            color: #ccc;
        }}
        
        .results-section {{
            display: none;
        }}
        
        .results-header {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
        }}
        
        .results-count {{
            color: #ccc;
            font-size: 1.1rem;
        }}
        
        .results-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
            gap: 20px;
        }}
        
        .result-card {{
            background: #1a1a1a;
            border-radius: 8px;
            overflow: hidden;
            transition: transform 0.3s, box-shadow 0.3s;
            cursor: pointer;
        }}
        
        .result-card:hover {{
            transform: translateY(-5px);
            box-shadow: 0 8px 16px rgba(0, 164, 220, 0.3);
        }}
        
        .result-poster {{
            width: 100%;
            height: 300px;
            object-fit: cover;
            background: #333;
        }}
        
        .result-info {{
            padding: 15px;
        }}
        
        .result-title {{
            font-size: 1.1rem;
            font-weight: bold;
            margin-bottom: 8px;
            color: #fff;
        }}
        
        .result-year {{
            color: #ccc;
            font-size: 0.9rem;
            margin-bottom: 8px;
        }}
        
        .result-rating {{
            display: flex;
            align-items: center;
            gap: 5px;
            color: #00a4dc;
            font-size: 0.9rem;
        }}
        
        .result-overview {{
            color: #ccc;
            font-size: 0.9rem;
            margin-top: 10px;
            display: -webkit-box;
            -webkit-line-clamp: 3;
            -webkit-box-orient: vertical;
            overflow: hidden;
        }}
        
        .error {{
            background: #ff4444;
            color: white;
            padding: 15px;
            border-radius: 6px;
            margin: 20px 0;
        }}
        
        .no-results {{
            text-align: center;
            padding: 40px;
            color: #ccc;
        }}
        
        .config-warning {{
            background: #ff9800;
            color: #000;
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
            text-align: center;
        }}
        
        .config-warning a {{
            color: #000;
            text-decoration: underline;
        }}
        
        @media (max-width: 768px) {{
            .search-form {{
                flex-direction: column;
            }}
            
            .search-input {{
                min-width: auto;
            }}
            
            .results-grid {{
                grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
                gap: 15px;
            }}
            
            .result-poster {{
                height: 225px;
            }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎬 JellyScout</h1>
            <p>Discover movies and TV shows powered by The Movie Database</p>
        </div>
        
        {(hasApiKey ? "" : @"
        <div class=""config-warning"">
            ⚠️ TMDB API key not configured. Please configure your API key in the 
            <a href=""/web/index.html#!/configurationpage?name=JellyScout%20Configuration"">plugin settings</a> 
            to use search functionality.
        </div>
        ")}
        
        <div class=""search-section"">
            <form class=""search-form"" id=""searchForm"">
                <input 
                    type=""text"" 
                    id=""searchInput"" 
                    class=""search-input"" 
                    placeholder=""Search for movies and TV shows..."" 
                    {(hasApiKey ? "" : "disabled")}
                />
                <select id=""searchType"" class=""search-type"" {(hasApiKey ? "" : "disabled")}>
                    <option value=""all"">All Content</option>
                    <option value=""movie"">Movies Only</option>
                    <option value=""tv"">TV Shows Only</option>
                </select>
                <button type=""submit"" class=""search-btn"" id=""searchBtn"" {(hasApiKey ? "" : "disabled")}>
                    Search
                </button>
            </form>
        </div>
        
        <div id=""loading"" class=""loading"" style=""display: none;"">
            <p>🔍 Searching...</p>
        </div>
        
        <div id=""error"" class=""error"" style=""display: none;""></div>
        
        <div id=""results"" class=""results-section"">
            <div class=""results-header"">
                <h2>Search Results</h2>
                <span id=""resultsCount"" class=""results-count""></span>
            </div>
            <div id=""resultsGrid"" class=""results-grid""></div>
        </div>
        
        <div id=""noResults"" class=""no-results"" style=""display: none;"">
            <p>No results found. Try a different search term.</p>
        </div>
    </div>
    
    <script>
        const searchForm = document.getElementById('searchForm');
        const searchInput = document.getElementById('searchInput');
        const searchType = document.getElementById('searchType');
        const searchBtn = document.getElementById('searchBtn');
        const loading = document.getElementById('loading');
        const error = document.getElementById('error');
        const results = document.getElementById('results');
        const resultsGrid = document.getElementById('resultsGrid');
        const resultsCount = document.getElementById('resultsCount');
        const noResults = document.getElementById('noResults');
        
        let currentSearch = '';
        
        // Check if API key is configured
        const hasApiKey = {hasApiKey.ToString().ToLower()};
        
        if (!hasApiKey) {{
            searchInput.disabled = true;
            searchType.disabled = true;
            searchBtn.disabled = true;
        }}
        
        searchForm.addEventListener('submit', async (e) => {{
            e.preventDefault();
            
            const query = searchInput.value.trim();
            const type = searchType.value;
            
            if (!query) {{
                showError('Please enter a search term');
                return;
            }}
            
            currentSearch = query;
            await performSearch(query, type);
        }});
        
        async function performSearch(query, type) {{
            showLoading();
            hideError();
            hideResults();
            
            try {{
                let url;
                if (type === 'all') {{
                    url = `/api/jellyscout/search/all?query=${{encodeURIComponent(query)}}`;
                }} else {{
                    url = `/api/jellyscout/search/${{type}}?query=${{encodeURIComponent(query)}}`;
                }}
                
                const response = await fetch(url);
                
                if (!response.ok) {{
                    throw new Error(`HTTP error! status: ${{response.status}}`);
                }}
                
                const data = await response.json();
                hideLoading();
                
                if (type === 'all') {{
                    const allResults = [...(data.movies || []), ...(data.tvShows || [])];
                    displayResults(allResults);
                }} else {{
                    displayResults(data);
                }}
                
            }} catch (err) {{
                hideLoading();
                showError(`Search failed: ${{err.message}}`);
            }}
        }}
        
        function displayResults(data) {{
            const items = Array.isArray(data) ? data : [];
            
            if (items.length === 0) {{
                showNoResults();
                return;
            }}
            
            resultsCount.textContent = `${{items.length}} result${{items.length !== 1 ? 's' : ''}} found`;
            resultsGrid.innerHTML = '';
            
            items.forEach(item => {{
                const card = createResultCard(item);
                resultsGrid.appendChild(card);
            }});
            
            showResults();
        }}
        
        function createResultCard(item) {{
            const card = document.createElement('div');
            card.className = 'result-card';
            
            const title = item.title || item.name || 'Unknown Title';
            const year = item.release_date || item.first_air_date || '';
            const yearText = year ? new Date(year).getFullYear() : '';
            const rating = item.vote_average ? item.vote_average.toFixed(1) : 'N/A';
            const overview = item.overview || 'No description available.';
            
            const posterUrl = item.poster_path 
                ? `https://image.tmdb.org/t/p/w300${{item.poster_path}}`
                : 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMzAwIiBoZWlnaHQ9IjQ1MCIgdmlld0JveD0iMCAwIDMwMCA0NTAiIGZpbGw9Im5vbmUiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+CjxyZWN0IHdpZHRoPSIzMDAiIGhlaWdodD0iNDUwIiBmaWxsPSIjMzMzIi8+Cjx0ZXh0IHg9IjE1MCIgeT0iMjI1IiB0ZXh0LWFuY2hvcj0ibWlkZGxlIiBmaWxsPSIjNjY2IiBmb250LXNpemU9IjE4Ij5ObyBJbWFnZTwvdGV4dD4KPC9zdmc+';
            
            card.innerHTML = `
                <img src=""${{posterUrl}}"" alt=""${{title}}"" class=""result-poster"" onerror=""this.src='data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMzAwIiBoZWlnaHQ9IjQ1MCIgdmlld0JveD0iMCAwIDMwMCA0NTAiIGZpbGw9Im5vbmUiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+CjxyZWN0IHdpZHRoPSIzMDAiIGhlaWdodD0iNDUwIiBmaWxsPSIjMzMzIi8+Cjx0ZXh0IHg9IjE1MCIgeT0iMjI1IiB0ZXh0LWFuY2hvcj0ibWlkZGxlIiBmaWxsPSIjNjY2IiBmb250LXNpemU9IjE4Ij5ObyBJbWFnZTwvdGV4dD4KPC9zdmc+'"">
                <div class=""result-info"">
                    <div class=""result-title"">${{title}}</div>
                    ${{yearText ? `<div class=""result-year"">${{yearText}}</div>` : ''}}
                    <div class=""result-rating"">⭐ ${{rating}}</div>
                    <div class=""result-overview"">${{overview}}</div>
                </div>
            `;
            
            return card;
        }}
        
        function showLoading() {{
            loading.style.display = 'block';
        }}
        
        function hideLoading() {{
            loading.style.display = 'none';
        }}
        
        function showError(message) {{
            error.textContent = message;
            error.style.display = 'block';
        }}
        
        function hideError() {{
            error.style.display = 'none';
        }}
        
        function showResults() {{
            results.style.display = 'block';
        }}
        
        function hideResults() {{
            results.style.display = 'none';
        }}
        
        function showNoResults() {{
            noResults.style.display = 'block';
        }}
        
        function hideNoResults() {{
            noResults.style.display = 'none';
        }}
        
        // Auto-focus search input
        if (hasApiKey) {{
            searchInput.focus();
        }}
        
        // Handle Enter key in search input
        searchInput.addEventListener('keypress', (e) => {{
            if (e.key === 'Enter') {{
                e.preventDefault();
                searchForm.dispatchEvent(new Event('submit'));
            }}
        }});
    </script>
</body>
</html>";
        }
    }
} 