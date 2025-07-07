using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for caching data to improve performance.
/// </summary>
public class CacheService
{
    private readonly ILogger<CacheService> _logger;
    private readonly ConcurrentDictionary<string, CacheItem> _cache;
    private readonly TimeSpan _defaultExpiry;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public CacheService(ILogger<CacheService> logger)
    {
        _logger = logger;
        _cache = new ConcurrentDictionary<string, CacheItem>();
        _defaultExpiry = TimeSpan.FromMinutes(30); // Default 30 minutes cache
    }

    /// <summary>
    /// Gets a cached item or computes it if not found or expired.
    /// </summary>
    /// <typeparam name="T">The type of the cached item.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The factory function to compute the value if not cached.</param>
    /// <param name="expiry">The cache expiry time.</param>
    /// <returns>The cached or computed value.</returns>
    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiry = null) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Cache key cannot be null or empty");
            return await factory();
        }

        var expiryTime = expiry ?? _defaultExpiry;

        if (_cache.TryGetValue(key, out var cachedItem))
        {
            if (DateTime.UtcNow < cachedItem.ExpiryTime)
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return cachedItem.Value as T;
            }
            else
            {
                // Remove expired item
                _cache.TryRemove(key, out _);
                _logger.LogDebug("Cache expired for key: {Key}", key);
            }
        }

        try
        {
            var value = await factory();
            if (value != null)
            {
                var newItem = new CacheItem(value, DateTime.UtcNow.Add(expiryTime));
                _cache[key] = newItem;
                _logger.LogDebug("Cache set for key: {Key} with expiry: {Expiry}", key, newItem.ExpiryTime);
            }
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing factory function for cache key: {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Gets a cached item.
    /// </summary>
    /// <typeparam name="T">The type of the cached item.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value or null if not found or expired.</returns>
    public T? Get<T>(string key) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (_cache.TryGetValue(key, out var cachedItem))
        {
            if (DateTime.UtcNow < cachedItem.ExpiryTime)
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return cachedItem.Value as T;
            }
            else
            {
                // Remove expired item
                _cache.TryRemove(key, out _);
                _logger.LogDebug("Cache expired for key: {Key}", key);
            }
        }

        return null;
    }

    /// <summary>
    /// Sets a cached item.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="expiry">The cache expiry time.</param>
    public void Set(string key, object value, TimeSpan? expiry = null)
    {
        if (string.IsNullOrWhiteSpace(key) || value == null)
        {
            _logger.LogWarning("Cache key and value cannot be null");
            return;
        }

        var expiryTime = expiry ?? _defaultExpiry;
        var item = new CacheItem(value, DateTime.UtcNow.Add(expiryTime));
        _cache[key] = item;
        _logger.LogDebug("Cache set for key: {Key} with expiry: {Expiry}", key, item.ExpiryTime);
    }

    /// <summary>
    /// Removes a cached item.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>True if the item was removed.</returns>
    public bool Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var removed = _cache.TryRemove(key, out _);
        if (removed)
        {
            _logger.LogDebug("Cache removed for key: {Key}", key);
        }
        return removed;
    }

    /// <summary>
    /// Clears all cached items.
    /// </summary>
    public void Clear()
    {
        var count = _cache.Count;
        _cache.Clear();
        _logger.LogInformation("Cache cleared. Removed {Count} items", count);
    }

    /// <summary>
    /// Removes expired items from the cache.
    /// </summary>
    public void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in _cache)
        {
            if (now >= kvp.Value.ExpiryTime)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired cache items", expiredKeys.Count);
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Cache statistics.</returns>
    public CacheStatistics GetStatistics()
    {
        var now = DateTime.UtcNow;
        var total = _cache.Count;
        var expired = 0;

        foreach (var item in _cache.Values)
        {
            if (now >= item.ExpiryTime)
            {
                expired++;
            }
        }

        return new CacheStatistics
        {
            TotalItems = total,
            ExpiredItems = expired,
            ActiveItems = total - expired
        };
    }
}

/// <summary>
/// Represents a cached item with expiry time.
/// </summary>
internal class CacheItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CacheItem"/> class.
    /// </summary>
    /// <param name="value">The cached value.</param>
    /// <param name="expiryTime">The expiry time.</param>
    public CacheItem(object value, DateTime expiryTime)
    {
        Value = value;
        ExpiryTime = expiryTime;
    }

    /// <summary>
    /// Gets the cached value.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Gets the expiry time.
    /// </summary>
    public DateTime ExpiryTime { get; }
}

/// <summary>
/// Represents cache statistics.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Gets or sets the total number of items in cache.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets or sets the number of expired items in cache.
    /// </summary>
    public int ExpiredItems { get; set; }

    /// <summary>
    /// Gets or sets the number of active (non-expired) items in cache.
    /// </summary>
    public int ActiveItems { get; set; }
} 