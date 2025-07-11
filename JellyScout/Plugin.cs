using System;
using System.Collections.Generic;
using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Controller.Plugins;
using Jellyfin.Plugin.JellyScout.Configuration;
using Jellyfin.Plugin.JellyScout.Services;

namespace Jellyfin.Plugin.JellyScout
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public override string Name => "JellyScout";
        public override Guid Id => Guid.Parse("12345678-1234-5678-9012-123456789012");
        public override string Description => "Search for movies and TV shows using TMDB with Live TV integration";

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin? Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "JellyScout Configuration",
                    EmbeddedResourcePath = "Jellyfin.Plugin.JellyScout.JellyScout.Configuration.configPage.html"
                },
                new PluginPageInfo
                {
                    Name = "BitPlay Live TV",
                    EmbeddedResourcePath = "Jellyfin.Plugin.JellyScout.JellyScout.Web.liveTvPage.html"
                }
            };
        }
    }

    // Separate service registrator class with parameterless constructor
    public class JellyScoutServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, MediaBrowser.Controller.IServerApplicationHost applicationHost)
        {
            // Register TMDBService as a singleton
            serviceCollection.AddSingleton<TMDBService>();
            
            // Disable BitPlay Live TV service since Xtream plugin is overriding Live TV system
            // Instead, we'll provide direct streaming links through the web interface
            /*
            // Re-enable BitPlay Live TV service to provide Channel 2998 alongside Xtream plugin
            // This should coexist with other Live TV services like Xtream
            try
            {
                serviceCollection.AddSingleton<MediaBrowser.Controller.LiveTv.ILiveTvService, BitPlayLiveTvService>();
            }
            catch (Exception ex)
            {
                // Log the error but don't fail plugin loading if Live TV service can't be registered
                System.Diagnostics.Debug.WriteLine($"Warning: Could not register BitPlay Live TV service: {ex.Message}");
            }
            */
        }
    }
} 