using System;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.JellyScout.Models;

/// <summary>
/// Represents a Radarr movie.
/// </summary>
public class RadarrMovie
{
    /// <summary>
    /// Gets or sets the movie ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the movie title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title slug.
    /// </summary>
    public string TitleSlug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TMDB ID.
    /// </summary>
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the IMDB ID.
    /// </summary>
    public string ImdbId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the overview.
    /// </summary>
    public string Overview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime.
    /// </summary>
    public int Runtime { get; set; }

    /// <summary>
    /// Gets or sets the year.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the movie is monitored.
    /// </summary>
    public bool Monitored { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the movie has a file.
    /// </summary>
    public bool HasFile { get; set; }

    /// <summary>
    /// Gets or sets the images.
    /// </summary>
    public RadarrImage[] Images { get; set; } = Array.Empty<RadarrImage>();
}

/// <summary>
/// Represents a Radarr movie image.
/// </summary>
public class RadarrImage
{
    /// <summary>
    /// Gets or sets the cover type.
    /// </summary>
    public string CoverType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the image URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Represents a Radarr root folder.
/// </summary>
public class RadarrRootFolder
{
    /// <summary>
    /// Gets or sets the folder ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the folder path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the accessible status.
    /// </summary>
    public bool Accessible { get; set; }

    /// <summary>
    /// Gets or sets the free space.
    /// </summary>
    public long FreeSpace { get; set; }

    /// <summary>
    /// Gets or sets the total space.
    /// </summary>
    public long TotalSpace { get; set; }
}

/// <summary>
/// Represents a Radarr quality profile.
/// </summary>
public class RadarrQualityProfile
{
    /// <summary>
    /// Gets or sets the profile ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the profile name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the upgrade allowed status.
    /// </summary>
    public bool UpgradeAllowed { get; set; }

    /// <summary>
    /// Gets or sets the cutoff.
    /// </summary>
    public int Cutoff { get; set; }
}

/// <summary>
/// Represents a Radarr queue item.
/// </summary>
public class RadarrQueueItem
{
    /// <summary>
    /// Gets or sets the queue item ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the movie ID.
    /// </summary>
    public int MovieId { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the download status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time remaining.
    /// </summary>
    public TimeSpan? TimeLeft { get; set; }

    /// <summary>
    /// Gets or sets the download size.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the remaining size.
    /// </summary>
    public long SizeLeft { get; set; }

    /// <summary>
    /// Gets or sets the download progress.
    /// </summary>
    public decimal Progress { get; set; }
}

/// <summary>
/// Represents a Radarr queue response.
/// </summary>
public class RadarrQueueResponse
{
    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total records.
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Gets or sets the records.
    /// </summary>
    public RadarrQueueItem[] Records { get; set; } = Array.Empty<RadarrQueueItem>();
} 