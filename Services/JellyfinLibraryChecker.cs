using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout.Services;

public class JellyfinLibraryChecker
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<JellyfinLibraryChecker> _logger;

    public JellyfinLibraryChecker(
        ILibraryManager libraryManager,
        ILoggerFactory loggerFactory)
    {
        _libraryManager = libraryManager;
        _logger = loggerFactory.CreateLogger<JellyfinLibraryChecker>();
    }

    public Task<bool> ExistsAsync(string title, int? year = null)
    {
        _logger.LogDebug("Checking if \"{Title}\" ({Year}) exists in Jellyfin library", title, year);

        var normalizedTitle = Normalize(title);

        var items = _libraryManager.RootFolder
            .RecursiveChildren
            .Where(i => Normalize(i.Name) == normalizedTitle);

        if (year.HasValue)
        {
            items = items.Where(i => i.ProductionYear == year.Value);
        }

        bool exists = items.Any();

        _logger.LogInformation("Library check for \"{Title}\" ({Year}): {Exists}", title, year, exists);

        return Task.FromResult(exists);
    }

    private string Normalize(string? s)
    {
        return s?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
