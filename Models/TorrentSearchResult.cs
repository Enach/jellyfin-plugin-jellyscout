using System;

namespace JellyScout.Models
{
    /// <summary>
    /// Represents a torrent search result from Prowlarr.
    /// </summary>
    public class TorrentSearchResult
    {
        /// <summary>
        /// Gets or sets the torrent title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the download URL (usually the Prowlarr download endpoint).
        /// </summary>
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the magnet URL.
        /// </summary>
        public string MagnetUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets or sets the number of seeders.
        /// </summary>
        public int Seeders { get; set; }

        /// <summary>
        /// Gets or sets the number of leechers.
        /// </summary>
        public int Leechers { get; set; }

        /// <summary>
        /// Gets or sets the extracted quality (e.g., 1080p, 720p).
        /// </summary>
        public string Quality { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the torrent provider/tracker name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the publish date.
        /// </summary>
        public DateTime PublishDate { get; set; }

        /// <summary>
        /// Gets the file size in a human-readable format.
        /// </summary>
        public string FormattedSize
        {
            get
            {
                if (Size == 0) return "Unknown";
                
                var sizeInGB = Size / 1024.0 / 1024.0 / 1024.0;
                if (sizeInGB >= 1)
                    return $"{sizeInGB:F1} GB";
                    
                var sizeInMB = Size / 1024.0 / 1024.0;
                return $"{sizeInMB:F0} MB";
            }
        }

        /// <summary>
        /// Gets a value indicating whether this torrent is suitable for streaming.
        /// </summary>
        public bool IsStreamable => Seeders >= 5; // Minimum seeders for streaming

        /// <summary>
        /// Gets the torrent health rating as a string.
        /// </summary>
        public string HealthRating
        {
            get
            {
                if (Seeders >= 50) return "Excellent";
                if (Seeders >= 20) return "Good";
                if (Seeders >= 10) return "Fair";
                if (Seeders >= 5) return "Poor";
                return "Very Poor";
            }
        }
    }
} 