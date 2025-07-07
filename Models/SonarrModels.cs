using System;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.JellyScout.Models;

/// <summary>
/// Represents a Sonarr series.
/// </summary>
public class SonarrSeries
{
    /// <summary>
    /// Gets or sets the series ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the series title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title slug.
    /// </summary>
    public string TitleSlug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TVDB ID.
    /// </summary>
    public int? TvdbId { get; set; }

    /// <summary>
    /// Gets or sets the TMDB ID.
    /// </summary>
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the overview.
    /// </summary>
    public string Overview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the season count.
    /// </summary>
    public int SeasonCount { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the series is monitored.
    /// </summary>
    public bool Monitored { get; set; }

    /// <summary>
    /// Gets or sets the images.
    /// </summary>
    public SonarrImage[] Images { get; set; } = Array.Empty<SonarrImage>();
}

/// <summary>
/// Represents a Sonarr series image.
/// </summary>
public class SonarrImage
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
/// Represents a Sonarr root folder.
/// </summary>
public class SonarrRootFolder
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
/// Represents a Sonarr quality profile.
/// </summary>
public class SonarrQualityProfile
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
/// Represents a Sonarr queue item.
/// </summary>
public class SonarrQueueItem
{
    /// <summary>
    /// Gets or sets the queue item ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the series ID.
    /// </summary>
    public int SeriesId { get; set; }

    /// <summary>
    /// Gets or sets the episode ID.
    /// </summary>
    public int EpisodeId { get; set; }

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
/// Represents a Sonarr queue response.
/// </summary>
public class SonarrQueueResponse
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
    public SonarrQueueItem[] Records { get; set; } = Array.Empty<SonarrQueueItem>();
}

/// <summary>
/// Represents a Sonarr episode.
/// </summary>
public class SonarrEpisode
{
    /// <summary>
    /// Gets or sets the episode ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the series ID.
    /// </summary>
    public int SeriesId { get; set; }

    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the episode has a file.
    /// </summary>
    public bool HasFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the episode is monitored.
    /// </summary>
    public bool Monitored { get; set; }

    /// <summary>
    /// Gets or sets the air date.
    /// </summary>
    public DateTime? AirDate { get; set; }

    /// <summary>
    /// Gets or sets the overview.
    /// </summary>
    public string Overview { get; set; } = string.Empty;
} 