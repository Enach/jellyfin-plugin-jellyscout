using System;
using System.Collections.Generic;
using Jellyfin.Plugin.JellyScout.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout;

/// <summary>
/// The main plugin class for JellyScout.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public override string Name => "JellyScout";

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    public override string Description => "Search & stream or request downloads from TMDB/Stremio with real-time notifications. Seamlessly discover and access media content.";

    /// <summary>
    /// Gets the plugin ID.
    /// </summary>
    public override Guid Id => Guid.Parse("a8b4d8c6-3f2a-4d7e-9b1c-5e8f2a9d3c7b");

    /// <summary>
    /// Gets the plugin configuration pages.
    /// </summary>
    /// <returns>The configuration pages.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "JellyScout Configuration",
                EmbeddedResourcePath = GetType().Namespace + ".Web.ConfigurationPage.html",
                EnableInMainMenu = false
            },
            new PluginPageInfo
            {
                Name = "JellyScout",
                EmbeddedResourcePath = GetType().Namespace + ".Web.index.html",
                EnableInMainMenu = true,
                MenuSection = "server",
                MenuIcon = "search"
            }
        };
    }
}
