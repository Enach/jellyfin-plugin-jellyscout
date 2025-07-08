using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyScout.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string TmdbApiKey { get; set; } = string.Empty;
        public int MaxSearchResults { get; set; } = 20;
        public string Language { get; set; } = "en";
        public string Region { get; set; } = "US";
        public bool IncludeAdult { get; set; } = false;
    }
} 