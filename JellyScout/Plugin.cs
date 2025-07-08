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
                }
            };
        }

        public void RegisterServices(IServiceCollection serviceCollection, MediaBrowser.Controller.IServerApplicationHost applicationHost)
        {
            // Register TMDBService as a singleton
            serviceCollection.AddSingleton<TMDBService>();
            
            // Register BitPlay Live TV service
            serviceCollection.AddSingleton<MediaBrowser.Controller.LiveTv.ILiveTvService, BitPlayLiveTvService>();
        }
    }
} 