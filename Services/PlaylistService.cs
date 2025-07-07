using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for managing user playlists and content queues.
/// </summary>
public class PlaylistService
{
    private readonly ILogger<PlaylistService> _logger;
    private readonly Dictionary<string, List<Playlist>> _userPlaylists = new();
    private readonly Dictionary<string, Queue<PlaylistItem>> _userQueues = new();
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaylistService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PlaylistService(ILogger<PlaylistService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a new playlist for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="name">The playlist name.</param>
    /// <param name="description">The playlist description.</param>
    /// <param name="isPublic">Whether the playlist is public.</param>
    /// <returns>The created playlist.</returns>
    public async Task<Playlist> CreatePlaylistAsync(string userId, string name, string? description = null, bool isPublic = false)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Playlist name cannot be empty", nameof(name));
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            if (!_userPlaylists.ContainsKey(userId))
            {
                _userPlaylists[userId] = new List<Playlist>();
            }

            var playlist = new Playlist
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Name = name,
                Description = description,
                IsPublic = isPublic,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Items = new List<PlaylistItem>()
            };

            _userPlaylists[userId].Add(playlist);
            _logger.LogInformation("Created playlist '{PlaylistName}' for user {UserId}", name, userId);

            return playlist;
        }
    }

    /// <summary>
    /// Gets all playlists for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The user's playlists.</returns>
    public async Task<List<Playlist>> GetUserPlaylistsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<Playlist>();
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            return _userPlaylists.TryGetValue(userId, out var playlists) 
                ? playlists.ToList() 
                : new List<Playlist>();
        }
    }

    /// <summary>
    /// Gets a specific playlist by ID.
    /// </summary>
    /// <param name="playlistId">The playlist ID.</param>
    /// <param name="userId">The user ID (for ownership validation).</param>
    /// <returns>The playlist if found and accessible.</returns>
    public async Task<Playlist?> GetPlaylistAsync(string playlistId, string userId)
    {
        if (string.IsNullOrWhiteSpace(playlistId) || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            return _userPlaylists.Values
                .SelectMany(playlists => playlists)
                .FirstOrDefault(p => p.Id == playlistId && (p.UserId == userId || p.IsPublic));
        }
    }

    /// <summary>
    /// Adds an item to a playlist.
    /// </summary>
    /// <param name="playlistId">The playlist ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="tmdbId">The TMDB ID of the content.</param>
    /// <param name="mediaType">The media type (movie, tv).</param>
    /// <param name="title">The content title.</param>
    /// <param name="posterPath">The poster path.</param>
    /// <returns>True if added successfully.</returns>
    public async Task<bool> AddToPlaylistAsync(string playlistId, string userId, int tmdbId, string mediaType, string title, string? posterPath = null)
    {
        if (string.IsNullOrWhiteSpace(playlistId) || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            var playlist = _userPlaylists.Values
                .SelectMany(playlists => playlists)
                .FirstOrDefault(p => p.Id == playlistId && p.UserId == userId);

            if (playlist == null)
            {
                _logger.LogWarning("Playlist {PlaylistId} not found or not accessible for user {UserId}", playlistId, userId);
                return false;
            }

            // Check if item already exists
            if (playlist.Items.Any(i => i.TmdbId == tmdbId && i.MediaType == mediaType))
            {
                _logger.LogInformation("Item {TmdbId} already exists in playlist {PlaylistId}", tmdbId, playlistId);
                return false;
            }

            var item = new PlaylistItem
            {
                Id = Guid.NewGuid().ToString(),
                TmdbId = tmdbId,
                MediaType = mediaType,
                Title = title,
                PosterPath = posterPath,
                AddedAt = DateTime.UtcNow,
                Position = playlist.Items.Count
            };

            playlist.Items.Add(item);
            playlist.UpdatedAt = DateTime.UtcNow;
            playlist.ItemCount = playlist.Items.Count;

            _logger.LogInformation("Added item '{Title}' to playlist '{PlaylistName}'", title, playlist.Name);
            return true;
        }
    }

    /// <summary>
    /// Removes an item from a playlist.
    /// </summary>
    /// <param name="playlistId">The playlist ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="itemId">The item ID to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public async Task<bool> RemoveFromPlaylistAsync(string playlistId, string userId, string itemId)
    {
        if (string.IsNullOrWhiteSpace(playlistId) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            var playlist = _userPlaylists.Values
                .SelectMany(playlists => playlists)
                .FirstOrDefault(p => p.Id == playlistId && p.UserId == userId);

            if (playlist == null)
            {
                return false;
            }

            var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
            {
                return false;
            }

            playlist.Items.Remove(item);
            playlist.UpdatedAt = DateTime.UtcNow;
            playlist.ItemCount = playlist.Items.Count;

            // Update positions
            for (int i = 0; i < playlist.Items.Count; i++)
            {
                playlist.Items[i].Position = i;
            }

            _logger.LogInformation("Removed item '{Title}' from playlist '{PlaylistName}'", item.Title, playlist.Name);
            return true;
        }
    }

    /// <summary>
    /// Reorders items in a playlist.
    /// </summary>
    /// <param name="playlistId">The playlist ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="itemId">The item ID to move.</param>
    /// <param name="newPosition">The new position.</param>
    /// <returns>True if reordered successfully.</returns>
    public async Task<bool> ReorderPlaylistItemAsync(string playlistId, string userId, string itemId, int newPosition)
    {
        if (string.IsNullOrWhiteSpace(playlistId) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            var playlist = _userPlaylists.Values
                .SelectMany(playlists => playlists)
                .FirstOrDefault(p => p.Id == playlistId && p.UserId == userId);

            if (playlist == null)
            {
                return false;
            }

            var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
            if (item == null || newPosition < 0 || newPosition >= playlist.Items.Count)
            {
                return false;
            }

            playlist.Items.Remove(item);
            playlist.Items.Insert(newPosition, item);

            // Update all positions
            for (int i = 0; i < playlist.Items.Count; i++)
            {
                playlist.Items[i].Position = i;
            }

            playlist.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Reordered item '{Title}' in playlist '{PlaylistName}' to position {Position}", 
                item.Title, playlist.Name, newPosition);

            return true;
        }
    }

    /// <summary>
    /// Deletes a playlist.
    /// </summary>
    /// <param name="playlistId">The playlist ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>True if deleted successfully.</returns>
    public async Task<bool> DeletePlaylistAsync(string playlistId, string userId)
    {
        if (string.IsNullOrWhiteSpace(playlistId) || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            if (!_userPlaylists.ContainsKey(userId))
            {
                return false;
            }

            var playlist = _userPlaylists[userId].FirstOrDefault(p => p.Id == playlistId);
            if (playlist == null)
            {
                return false;
            }

            _userPlaylists[userId].Remove(playlist);
            _logger.LogInformation("Deleted playlist '{PlaylistName}' for user {UserId}", playlist.Name, userId);

            return true;
        }
    }

    /// <summary>
    /// Gets the current streaming queue for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The current queue items.</returns>
    public async Task<List<PlaylistItem>> GetQueueAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<PlaylistItem>();
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            return _userQueues.TryGetValue(userId, out var queue) 
                ? queue.ToList() 
                : new List<PlaylistItem>();
        }
    }

    /// <summary>
    /// Adds an item to the streaming queue.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="item">The item to add.</param>
    /// <returns>True if added successfully.</returns>
    public async Task<bool> AddToQueueAsync(string userId, PlaylistItem item)
    {
        if (string.IsNullOrWhiteSpace(userId) || item == null)
        {
            return false;
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            if (!_userQueues.ContainsKey(userId))
            {
                _userQueues[userId] = new Queue<PlaylistItem>();
            }

            _userQueues[userId].Enqueue(item);
            _logger.LogInformation("Added item '{Title}' to queue for user {UserId}", item.Title, userId);

            return true;
        }
    }

    /// <summary>
    /// Gets the next item from the streaming queue.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The next item in the queue.</returns>
    public async Task<PlaylistItem?> GetNextFromQueueAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            if (_userQueues.TryGetValue(userId, out var queue) && queue.Count > 0)
            {
                var item = queue.Dequeue();
                _logger.LogInformation("Retrieved next item '{Title}' from queue for user {UserId}", item.Title, userId);
                return item;
            }

            return null;
        }
    }

    /// <summary>
    /// Clears the streaming queue for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>True if cleared successfully.</returns>
    public async Task<bool> ClearQueueAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            if (_userQueues.ContainsKey(userId))
            {
                _userQueues[userId].Clear();
                _logger.LogInformation("Cleared queue for user {UserId}", userId);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Adds all items from a playlist to the streaming queue.
    /// </summary>
    /// <param name="playlistId">The playlist ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="shuffle">Whether to shuffle the playlist.</param>
    /// <returns>True if added successfully.</returns>
    public async Task<bool> PlayPlaylistAsync(string playlistId, string userId, bool shuffle = false)
    {
        if (string.IsNullOrWhiteSpace(playlistId) || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var playlist = await GetPlaylistAsync(playlistId, userId);
        if (playlist == null || !playlist.Items.Any())
        {
            return false;
        }

        lock (_lock)
        {
            if (!_userQueues.ContainsKey(userId))
            {
                _userQueues[userId] = new Queue<PlaylistItem>();
            }

            var items = playlist.Items.ToList();
            if (shuffle)
            {
                var random = new Random();
                items = items.OrderBy(x => random.Next()).ToList();
            }

            foreach (var item in items)
            {
                _userQueues[userId].Enqueue(item);
            }

            _logger.LogInformation("Added {Count} items from playlist '{PlaylistName}' to queue for user {UserId} (shuffle: {Shuffle})", 
                items.Count, playlist.Name, userId, shuffle);

            return true;
        }
    }

    /// <summary>
    /// Gets playlist statistics.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Playlist statistics.</returns>
    public async Task<PlaylistStats> GetStatsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new PlaylistStats();
        }

        await Task.CompletedTask; // Future-proofing for async operations

        lock (_lock)
        {
            if (!_userPlaylists.ContainsKey(userId))
            {
                return new PlaylistStats();
            }

            var playlists = _userPlaylists[userId];
            var totalItems = playlists.Sum(p => p.ItemCount);
            var queueSize = _userQueues.TryGetValue(userId, out var queue) ? queue.Count : 0;

            return new PlaylistStats
            {
                TotalPlaylists = playlists.Count,
                TotalItems = totalItems,
                QueueSize = queueSize,
                PublicPlaylists = playlists.Count(p => p.IsPublic),
                PrivatePlaylists = playlists.Count(p => !p.IsPublic)
            };
        }
    }
}

/// <summary>
/// Represents a user playlist.
/// </summary>
public class Playlist
{
    /// <summary>
    /// Gets or sets the playlist ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID who owns this playlist.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the playlist name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the playlist description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether the playlist is public.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update date.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the playlist items.
    /// </summary>
    public List<PlaylistItem> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the item count.
    /// </summary>
    public int ItemCount { get; set; }
}

/// <summary>
/// Represents an item in a playlist.
/// </summary>
public class PlaylistItem
{
    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TMDB ID.
    /// </summary>
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets the media type (movie, tv).
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the poster path.
    /// </summary>
    public string? PosterPath { get; set; }

    /// <summary>
    /// Gets or sets the position in the playlist.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets when the item was added.
    /// </summary>
    public DateTime AddedAt { get; set; }
}

/// <summary>
/// Playlist statistics.
/// </summary>
public class PlaylistStats
{
    /// <summary>
    /// Gets or sets the total number of playlists.
    /// </summary>
    public int TotalPlaylists { get; set; }

    /// <summary>
    /// Gets or sets the total number of items across all playlists.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets or sets the current queue size.
    /// </summary>
    public int QueueSize { get; set; }

    /// <summary>
    /// Gets or sets the number of public playlists.
    /// </summary>
    public int PublicPlaylists { get; set; }

    /// <summary>
    /// Gets or sets the number of private playlists.
    /// </summary>
    public int PrivatePlaylists { get; set; }
} 