# JellyScout Plugin for Jellyfin

A simple, clean Jellyfin plugin that provides movie and TV show search functionality using The Movie Database (TMDB) API. JellyScout integrates seamlessly with Jellyfin's native Live TV interface.

## Features

- **TMDB Search**: Search for movies and TV shows using TMDB's comprehensive database
- **Live TV Integration**: Appears as channels in Jellyfin's Live TV interface
- **REST API**: Direct API access for search functionality
- **Simple Configuration**: Easy setup with just a TMDB API key
- **Multi-language Support**: Search in multiple languages with regional preferences

## Installation

1. Download the latest release from the [releases page](https://github.com/jellyfin/jellyfin-plugin-jellyscout/releases)
2. Extract the plugin files to your Jellyfin plugins directory
3. Restart Jellyfin
4. Configure the plugin in the admin dashboard

## Configuration

1. Go to **Admin Dashboard** → **Plugins** → **JellyScout**
2. Enter your TMDB API key (get one free from [TMDB](https://www.themoviedb.org/settings/api))
3. Configure your preferences:
   - **Max Search Results**: Number of results to return (10-100)
   - **Language**: Language for search results and metadata
   - **Region**: Region for content filtering and release dates
   - **Include Adult Content**: Whether to include adult content in results
4. Click **Save**

## Usage

### Live TV Interface

After installation and configuration, JellyScout appears as two channels in Jellyfin's Live TV:

- **Channel 1001**: Movie Search
- **Channel 1002**: TV Show Search

Navigate to **Live TV** in Jellyfin and select the JellyScout channels to access search functionality.

### REST API

You can also access search functionality directly through the REST API:

```bash
# Search for movies and TV shows
GET /api/jellyscout/search?query=batman

# Search for movies only
GET /api/jellyscout/search?query=batman&mediaType=movie

# Search for TV shows only
GET /api/jellyscout/search?query=batman&mediaType=tv

# Get plugin status
GET /api/jellyscout/status
```

## Architecture

JellyScout follows a simple, clean architecture:

```
JellyScout/
├── Configuration/
│   ├── PluginConfiguration.cs    # Plugin settings
│   └── configPage.html           # Configuration web page
├── Controllers/
│   └── JellyScoutController.cs   # REST API endpoints
├── Models/
│   └── TmdbModels.cs             # TMDB data models
├── Services/
│   ├── TMDBService.cs            # TMDB API service
│   ├── TmdbLiveTvService.cs      # Live TV integration
│   └── JellyScoutServiceRegistrator.cs # DI registration
└── Plugin.cs                     # Main plugin class
```

### Key Components

- **TMDBService**: Handles all TMDB API interactions
- **TmdbLiveTvService**: Implements `ILiveTvService` for Live TV integration
- **JellyScoutController**: Provides REST API endpoints
- **PluginConfiguration**: Simple configuration with essential settings

## Development

### Building from Source

1. Clone the repository
2. Ensure you have .NET 6.0 or later installed
3. Build the project:
   ```bash
   dotnet build
   ```
4. The plugin DLL will be in `bin/Debug/net6.0/`

### Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## Requirements

- Jellyfin 10.8.0 or later
- TMDB API key (free registration required)
- Internet connection for TMDB API access

## Changelog

### Version 1.0.0
- Complete rewrite with clean, simple architecture
- Live TV integration following Jellyfin.Xtream pattern
- Removed over-engineered components
- Simple TMDB search functionality
- REST API endpoints
- Clean configuration interface

## License

This plugin is licensed under the GPL-3.0 License. See the [LICENSE](LICENSE) file for details.

## Support

For issues, questions, or feature requests, please use the [GitHub Issues](https://github.com/jellyfin/jellyfin-plugin-jellyscout/issues) page.

## Credits

- [The Movie Database (TMDB)](https://www.themoviedb.org/) for the movie and TV show data
- [Jellyfin](https://jellyfin.org/) for the media server platform
- [Jellyfin.Xtream](https://github.com/Kevinjil/Jellyfin.Xtream) for Live TV integration patterns 