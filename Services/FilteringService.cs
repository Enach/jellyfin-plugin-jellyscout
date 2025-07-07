using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for advanced filtering and sorting of search results.
/// </summary>
public class FilteringService
{
    private readonly ILogger<FilteringService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilteringService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public FilteringService(ILogger<FilteringService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Applies advanced filtering and sorting to search results.
    /// </summary>
    /// <param name="results">The search results to filter.</param>
    /// <param name="filters">The filter criteria.</param>
    /// <returns>Filtered and sorted results.</returns>
    public IEnumerable<JObject> ApplyFiltersAndSorting(IEnumerable<JObject> results, SearchFilters filters)
    {
        if (results == null)
        {
            return Enumerable.Empty<JObject>();
        }

        var filteredResults = results.AsEnumerable();

        // Apply filters
        filteredResults = ApplyGenreFilter(filteredResults, filters.Genres);
        filteredResults = ApplyYearFilter(filteredResults, filters.YearFrom, filters.YearTo);
        filteredResults = ApplyRatingFilter(filteredResults, filters.MinRating, filters.MaxRating);
        filteredResults = ApplyMediaTypeFilter(filteredResults, filters.MediaTypes);
        filteredResults = ApplyLanguageFilter(filteredResults, filters.Languages);
        filteredResults = ApplyRuntimeFilter(filteredResults, filters.MinRuntime, filters.MaxRuntime);

        // Apply sorting
        filteredResults = ApplySorting(filteredResults, filters.SortBy, filters.SortOrder);

        // Apply pagination
        if (filters.Page > 0 && filters.PageSize > 0)
        {
            var skip = (filters.Page - 1) * filters.PageSize;
            filteredResults = filteredResults.Skip(skip).Take(filters.PageSize);
        }

        _logger.LogDebug("Applied filters and sorting. Original count: {OriginalCount}, Filtered count: {FilteredCount}", 
            results.Count(), filteredResults.Count());

        return filteredResults;
    }

    /// <summary>
    /// Gets available filter options from a collection of results.
    /// </summary>
    /// <param name="results">The search results.</param>
    /// <returns>Available filter options.</returns>
    public FilterOptions GetAvailableFilters(IEnumerable<JObject> results)
    {
        if (results == null || !results.Any())
        {
            return new FilterOptions();
        }

        var genres = new HashSet<string>();
        var languages = new HashSet<string>();
        var years = new List<int>();
        var ratings = new List<double>();

        foreach (var result in results)
        {
            // Extract genres
            var genreArray = result["genre_ids"]?.ToObject<int[]>();
            if (genreArray != null)
            {
                foreach (var genreId in genreArray)
                {
                    var genreName = GetGenreName(genreId);
                    if (!string.IsNullOrEmpty(genreName))
                    {
                        genres.Add(genreName);
                    }
                }
            }

            // Extract year
            var releaseDate = result["release_date"]?.ToString() ?? result["first_air_date"]?.ToString();
            if (DateTime.TryParse(releaseDate, out var date))
            {
                years.Add(date.Year);
            }

            // Extract rating
            var rating = result["vote_average"]?.Value<double>();
            if (rating.HasValue)
            {
                ratings.Add(rating.Value);
            }

            // Extract language
            var language = result["original_language"]?.ToString();
            if (!string.IsNullOrEmpty(language))
            {
                languages.Add(language);
            }
        }

        return new FilterOptions
        {
            AvailableGenres = genres.OrderBy(g => g).ToList(),
            AvailableLanguages = languages.OrderBy(l => l).ToList(),
            YearRange = years.Any() ? new YearRange { Min = years.Min(), Max = years.Max() } : new YearRange(),
            RatingRange = ratings.Any() ? new RatingRange { Min = ratings.Min(), Max = ratings.Max() } : new RatingRange()
        };
    }

    private IEnumerable<JObject> ApplyGenreFilter(IEnumerable<JObject> results, List<string>? genres)
    {
        if (genres == null || !genres.Any())
        {
            return results;
        }

        return results.Where(result =>
        {
            var genreIds = result["genre_ids"]?.ToObject<int[]>() ?? Array.Empty<int>();
            var resultGenres = genreIds.Select(GetGenreName).Where(g => !string.IsNullOrEmpty(g));
            return genres.Any(g => resultGenres.Contains(g, StringComparer.OrdinalIgnoreCase));
        });
    }

    private IEnumerable<JObject> ApplyYearFilter(IEnumerable<JObject> results, int? yearFrom, int? yearTo)
    {
        if (!yearFrom.HasValue && !yearTo.HasValue)
        {
            return results;
        }

        return results.Where(result =>
        {
            var releaseDate = result["release_date"]?.ToString() ?? result["first_air_date"]?.ToString();
            if (DateTime.TryParse(releaseDate, out var date))
            {
                if (yearFrom.HasValue && date.Year < yearFrom.Value)
                    return false;
                if (yearTo.HasValue && date.Year > yearTo.Value)
                    return false;
                return true;
            }
            return !yearFrom.HasValue && !yearTo.HasValue;
        });
    }

    private IEnumerable<JObject> ApplyRatingFilter(IEnumerable<JObject> results, double? minRating, double? maxRating)
    {
        if (!minRating.HasValue && !maxRating.HasValue)
        {
            return results;
        }

        return results.Where(result =>
        {
            var rating = result["vote_average"]?.Value<double>();
            if (rating.HasValue)
            {
                if (minRating.HasValue && rating.Value < minRating.Value)
                    return false;
                if (maxRating.HasValue && rating.Value > maxRating.Value)
                    return false;
                return true;
            }
            return !minRating.HasValue && !maxRating.HasValue;
        });
    }

    private IEnumerable<JObject> ApplyMediaTypeFilter(IEnumerable<JObject> results, List<string>? mediaTypes)
    {
        if (mediaTypes == null || !mediaTypes.Any())
        {
            return results;
        }

        return results.Where(result =>
        {
            var mediaType = result["media_type"]?.ToString();
            return mediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase);
        });
    }

    private IEnumerable<JObject> ApplyLanguageFilter(IEnumerable<JObject> results, List<string>? languages)
    {
        if (languages == null || !languages.Any())
        {
            return results;
        }

        return results.Where(result =>
        {
            var language = result["original_language"]?.ToString();
            return languages.Contains(language, StringComparer.OrdinalIgnoreCase);
        });
    }

    private IEnumerable<JObject> ApplyRuntimeFilter(IEnumerable<JObject> results, int? minRuntime, int? maxRuntime)
    {
        if (!minRuntime.HasValue && !maxRuntime.HasValue)
        {
            return results;
        }

        return results.Where(result =>
        {
            var runtime = result["runtime"]?.Value<int>();
            if (runtime.HasValue)
            {
                if (minRuntime.HasValue && runtime.Value < minRuntime.Value)
                    return false;
                if (maxRuntime.HasValue && runtime.Value > maxRuntime.Value)
                    return false;
                return true;
            }
            return !minRuntime.HasValue && !maxRuntime.HasValue;
        });
    }

    private IEnumerable<JObject> ApplySorting(IEnumerable<JObject> results, SortOption sortBy, SortOrder sortOrder)
    {
        var sortedResults = sortBy switch
        {
            SortOption.Popularity => results.OrderBy(r => r["popularity"]?.Value<double>() ?? 0),
            SortOption.Rating => results.OrderBy(r => r["vote_average"]?.Value<double>() ?? 0),
            SortOption.ReleaseDate => results.OrderBy(r => GetReleaseDate(r)),
            SortOption.Title => results.OrderBy(r => GetTitle(r)),
            SortOption.VoteCount => results.OrderBy(r => r["vote_count"]?.Value<int>() ?? 0),
            _ => results.OrderBy(r => r["popularity"]?.Value<double>() ?? 0)
        };

        return sortOrder == SortOrder.Descending ? sortedResults.Reverse() : sortedResults;
    }

    private DateTime GetReleaseDate(JObject result)
    {
        var releaseDate = result["release_date"]?.ToString() ?? result["first_air_date"]?.ToString();
        return DateTime.TryParse(releaseDate, out var date) ? date : DateTime.MinValue;
    }

    private string GetTitle(JObject result)
    {
        return result["title"]?.ToString() ?? result["name"]?.ToString() ?? string.Empty;
    }

    private string GetGenreName(int genreId)
    {
        // TMDB Genre mapping - this could be moved to a separate service or config
        var genres = new Dictionary<int, string>
        {
            { 28, "Action" }, { 12, "Adventure" }, { 16, "Animation" }, { 35, "Comedy" },
            { 80, "Crime" }, { 99, "Documentary" }, { 18, "Drama" }, { 10751, "Family" },
            { 14, "Fantasy" }, { 36, "History" }, { 27, "Horror" }, { 10402, "Music" },
            { 9648, "Mystery" }, { 10749, "Romance" }, { 878, "Science Fiction" },
            { 10770, "TV Movie" }, { 53, "Thriller" }, { 10752, "War" }, { 37, "Western" },
            { 10759, "Action & Adventure" }, { 10762, "Kids" }, { 10763, "News" },
            { 10764, "Reality" }, { 10765, "Sci-Fi & Fantasy" }, { 10766, "Soap" },
            { 10767, "Talk" }, { 10768, "War & Politics" }
        };

        return genres.TryGetValue(genreId, out var genre) ? genre : string.Empty;
    }
}

/// <summary>
/// Search filter criteria.
/// </summary>
public class SearchFilters
{
    /// <summary>
    /// Gets or sets the genres to filter by.
    /// </summary>
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// Gets or sets the media types to filter by (movie, tv).
    /// </summary>
    public List<string> MediaTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets the languages to filter by.
    /// </summary>
    public List<string> Languages { get; set; } = new();

    /// <summary>
    /// Gets or sets the minimum year for filtering.
    /// </summary>
    public int? YearFrom { get; set; }

    /// <summary>
    /// Gets or sets the maximum year for filtering.
    /// </summary>
    public int? YearTo { get; set; }

    /// <summary>
    /// Gets or sets the minimum rating for filtering.
    /// </summary>
    public double? MinRating { get; set; }

    /// <summary>
    /// Gets or sets the maximum rating for filtering.
    /// </summary>
    public double? MaxRating { get; set; }

    /// <summary>
    /// Gets or sets the minimum runtime in minutes.
    /// </summary>
    public int? MinRuntime { get; set; }

    /// <summary>
    /// Gets or sets the maximum runtime in minutes.
    /// </summary>
    public int? MaxRuntime { get; set; }

    /// <summary>
    /// Gets or sets the sort option.
    /// </summary>
    public SortOption SortBy { get; set; } = SortOption.Popularity;

    /// <summary>
    /// Gets or sets the sort order.
    /// </summary>
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;

    /// <summary>
    /// Gets or sets the page number for pagination.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size for pagination.
    /// </summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Available filter options.
/// </summary>
public class FilterOptions
{
    /// <summary>
    /// Gets or sets the available genres.
    /// </summary>
    public List<string> AvailableGenres { get; set; } = new();

    /// <summary>
    /// Gets or sets the available languages.
    /// </summary>
    public List<string> AvailableLanguages { get; set; } = new();

    /// <summary>
    /// Gets or sets the year range.
    /// </summary>
    public YearRange YearRange { get; set; } = new();

    /// <summary>
    /// Gets or sets the rating range.
    /// </summary>
    public RatingRange RatingRange { get; set; } = new();
}

/// <summary>
/// Year range for filtering.
/// </summary>
public class YearRange
{
    /// <summary>
    /// Gets or sets the minimum year.
    /// </summary>
    public int Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum year.
    /// </summary>
    public int Max { get; set; }
}

/// <summary>
/// Rating range for filtering.
/// </summary>
public class RatingRange
{
    /// <summary>
    /// Gets or sets the minimum rating.
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum rating.
    /// </summary>
    public double Max { get; set; }
}

/// <summary>
/// Sort options for search results.
/// </summary>
public enum SortOption
{
    /// <summary>
    /// Sort by popularity.
    /// </summary>
    Popularity,

    /// <summary>
    /// Sort by rating.
    /// </summary>
    Rating,

    /// <summary>
    /// Sort by release date.
    /// </summary>
    ReleaseDate,

    /// <summary>
    /// Sort by title.
    /// </summary>
    Title,

    /// <summary>
    /// Sort by vote count.
    /// </summary>
    VoteCount
}

/// <summary>
/// Sort order options.
/// </summary>
public enum SortOrder
{
    /// <summary>
    /// Ascending order.
    /// </summary>
    Ascending,

    /// <summary>
    /// Descending order.
    /// </summary>
    Descending
} 