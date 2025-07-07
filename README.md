# JellyScout - Jellyfin Content Discovery Plugin

A comprehensive Jellyfin plugin that enhances your media discovery experience with powerful search capabilities, content recommendations, and seamless integration with your existing media management tools.

## Features

- **Content Discovery**: Search and discover movies and TV shows
- **External Integrations**: Seamlessly connects with Sonarr, Radarr, Prowlarr, and BitPlay
- **Smart Filtering**: Advanced filtering options with quality preferences
- **Automatic Downloads**: Request content directly from Jellyfin interface
- **Health Monitoring**: Built-in health checks for all connected services
- **Caching**: Intelligent caching for improved performance
- **Modern UI**: Clean, responsive web interface

## Prerequisites

- Jellyfin server (version 10.8.0 or higher)
- Optionally: Sonarr, Radarr, Prowlarr, or BitPlay if you want those features

## Installation

1. Download the latest release from the [releases page](https://github.com/your-username/jellyfin-plugin-jellyscout/releases)
2. Extract the plugin files to your Jellyfin plugins directory:
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\JellyScout`
   - Linux: `/var/lib/jellyfin/plugins/JellyScout`
   - macOS: `/var/lib/jellyfin/plugins/JellyScout`
3. Restart your Jellyfin server
4. Navigate to **Dashboard > Plugins > JellyScout** to configure

## Configuration

### Basic Setup
1. Go to **Dashboard > Plugins > JellyScout**
2. Configure your external service integrations (optional)
3. Set your preferred quality and filtering options
4. Save the configuration

### External Service Integration

**Sonarr (for TV shows):**
- Server URL: `http://your-sonarr-server:8989`
- API Key: Your Sonarr API key

**Radarr (for movies):**
- Server URL: `http://your-radarr-server:7878`
- API Key: Your Radarr API key

**Prowlarr (for indexer management):**
- Server URL: `http://your-prowlarr-server:9696`
- API Key: Your Prowlarr API key

**BitPlay (for direct streaming):**
- Server URL: `http://your-bitplay-server:port`

### Quality Settings
- **Minimum Seeders**: Set minimum number of seeders for torrent results
- **Preferred Quality**: Choose default quality preference (720p, 1080p, 4K)
- **Max Results**: Maximum number of results to display per search

## Usage

### Content Discovery
1. Navigate to the **JellyScout** page in your Jellyfin interface
2. Use the search bar to find movies or TV shows
3. Apply filters to refine your results
4. Click on any result to view details and available actions

### Requesting Content
1. Find the content you want
2. Click **Request** to add it to your download queue
3. If configured, the request will be sent to Sonarr/Radarr
4. Monitor progress through the plugin interface

### Playlists
- Create custom playlists from search results
- Export playlists to external services
- Manage your content collections

## Troubleshooting

### Common Issues

**Plugin not appearing in Jellyfin:**
- Ensure the plugin is in the correct directory
- Check Jellyfin logs for loading errors
- Verify file permissions

**External services not connecting:**
- Verify server URLs are correct and accessible
- Check API keys are valid
- Ensure services are running and accessible from Jellyfin server

**No search results:**
- Verify Prowlarr integration is working
- Check if indexers are configured in Prowlarr
- Review plugin logs for error messages

### Health Checks
The plugin includes built-in health monitoring:
- Navigate to **Dashboard > Plugins > JellyScout > Health**
- View status of all connected services
- Check detailed error messages and suggestions

## Development

### Building from Source
```bash
git clone https://github.com/your-username/jellyfin-plugin-jellyscout.git
cd jellyfin-plugin-jellyscout
dotnet build
```

### Testing
```bash
dotnet test
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- **GitHub Issues**: [Report bugs or request features](https://github.com/your-username/jellyfin-plugin-jellyscout/issues)
- **Documentation**: [Wiki](https://github.com/your-username/jellyfin-plugin-jellyscout/wiki)

## Changelog

### v0.1.7 (Latest)
- Fixed configuration page settings not saving
- Changed Prowlarr API Key to text field for better UX
- Simplified configuration system using Jellyfin's built-in form handling
- Removed unnecessary complexity and custom JavaScript
- Updated all service integrations for better reliability

### v0.1.5
- Added comprehensive API integration with Sonarr, Radarr, and Prowlarr
- Implemented advanced filtering, playlist support, and configuration validation
- Enhanced error handling and logging
- Added health check system for monitoring service status
- Improved caching and performance optimization

### v0.1.0
- Initial release
- Basic content discovery functionality
- Integration with external services
- Web-based configuration interface 