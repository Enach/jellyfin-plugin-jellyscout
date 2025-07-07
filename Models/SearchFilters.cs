using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.JellyScout.Models;

/// <summary>
/// Represents search filters for advanced filtering.
/// </summary>
public class SearchFilters
{
    /// <summary>
    /// Gets or sets the minimum release year.
    /// </summary>
    public int? MinYear { get; set; }

    /// <summary>
    /// Gets or sets the maximum release year.
    /// </summary>
    public int? MaxYear { get; set; }

    /// <summary>
    /// Gets or sets the minimum rating.
    /// </summary>
    public double? MinRating { get; set; }

    /// <summary>
    /// Gets or sets the maximum rating.
    /// </summary>
    public double? MaxRating { get; set; }

    /// <summary>
    /// Gets or sets the media type filter (movie, tv).
    /// </summary>
    public string? MediaType { get; set; }

    /// <summary>
    /// Gets or sets the genre filters.
    /// </summary>
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// Gets or sets the language filter.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the country filter.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets the sort field.
    /// </summary>
    public string SortBy { get; set; } = "popularity";

    /// <summary>
    /// Gets or sets the sort order.
    /// </summary>
    public SortOrder SortOrder { get; set; } = SortOrder.Descending;

    /// <summary>
    /// Gets or sets whether to include adult content.
    /// </summary>
    public bool IncludeAdult { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to only show content in the library.
    /// </summary>
    public bool OnlyInLibrary { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to exclude content in the library.
    /// </summary>
    public bool ExcludeInLibrary { get; set; } = false;

    /// <summary>
    /// Gets or sets the minimum runtime in minutes.
    /// </summary>
    public int? MinRuntime { get; set; }

    /// <summary>
    /// Gets or sets the maximum runtime in minutes.
    /// </summary>
    public int? MaxRuntime { get; set; }

    /// <summary>
    /// Gets or sets the certification filter (G, PG, PG-13, R, etc.).
    /// </summary>
    public string? Certification { get; set; }

    /// <summary>
    /// Gets or sets the keywords to include.
    /// </summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// Gets or sets the keywords to exclude.
    /// </summary>
    public List<string> ExcludeKeywords { get; set; } = new();

    /// <summary>
    /// Gets or sets the cast members to include.
    /// </summary>
    public List<string> Cast { get; set; } = new();

    /// <summary>
    /// Gets or sets the crew members to include.
    /// </summary>
    public List<string> Crew { get; set; } = new();

    /// <summary>
    /// Gets or sets the production companies to include.
    /// </summary>
    public List<string> Companies { get; set; } = new();

    /// <summary>
    /// Gets or sets the network filter for TV shows.
    /// </summary>
    public string? Network { get; set; }

    /// <summary>
    /// Gets or sets the status filter for TV shows.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the TV show type filter.
    /// </summary>
    public string? TvType { get; set; }

    /// <summary>
    /// Gets or sets custom filters.
    /// </summary>
    public Dictionary<string, object> CustomFilters { get; set; } = new();
}

/// <summary>
/// Represents sort order options.
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