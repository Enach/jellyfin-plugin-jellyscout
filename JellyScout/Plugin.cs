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
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IPluginServiceRegistrator
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

        public void RegisterServices(IServiceCollection serviceCollection, MediaBrowser.Controller.IServerApplicationHost applicationHost)
        {
            // Register TMDBService as a singleton
            serviceCollection.AddSingleton<TMDBService>();
            
            // Temporarily disable BitPlay Live TV service to avoid conflicts with Xtream plugin
            // TODO: Re-enable once we figure out how to coexist with other Live TV plugins
            /*
            // Register BitPlay Live TV service - make it optional to avoid conflicts with other Live TV plugins
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