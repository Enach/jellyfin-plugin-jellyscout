# JellyScout - Jellyfin Plugin

A simple plugin that helps you discover and manage movies and TV shows in your Jellyfin server.

JellyScout connects to The Movie Database (TMDB) to let you search for content, see what's already in your library, and optionally stream or download new content through integrations with services like Sonarr and Radarr.

## What it does

- **Search**: Look up movies and TV shows using TMDB's database
- **Library check**: See if content is already in your Jellyfin library
- **Streaming**: Stream content directly (if you have Streamio set up)
- **Downloads**: Add content to Sonarr/Radarr for downloading
- **Notifications**: Get updates on what's happening

## Getting started

### What you need
- Jellyfin Server 10.9.0 or newer
- A free TMDB API key
- Optionally: Sonarr, Radarr, or Streamio if you want those features

### Installation

The easiest way is through Jellyfin's plugin catalog:

1. Go to Jellyfin Admin → Plugins → Catalog
2. Find "JellyScout" and install it
3. Restart Jellyfin
4. Configure the plugin with your TMDB API key

You can also install manually by downloading the latest release and copying the files to your Jellyfin plugins folder.

## Setting it up

### Getting a TMDB API key

1. Go to [TMDB](https://www.themoviedb.org) and create a free account
2. Go to Settings → API and request an API key
3. Choose "Developer" when asked about usage
4. Copy your API key

### Basic configuration

In Jellyfin Admin → Plugins → JellyScout:

- **TMDB API Key**: Paste your API key here (required)
- **Enable Streaming**: Turn on if you want to stream content
- **Enable Downloads**: Turn on if you want to download content
- **Enable Notifications**: Turn on if you want status updates

### Optional integrations

If you use these services, you can connect them:

**Sonarr (for TV shows):**
- URL: `http://your-sonarr-server:8989`
- API Key: Found in Sonarr's settings

**Radarr (for movies):**
- URL: `http://your-radarr-server:7878`
- API Key: Found in Radarr's settings

**Streamio (for streaming):**
- URL: `http://your-streamio-server:port`
- API Key: Your Streamio API key

## How to use it

1. Open the JellyScout page in Jellyfin
2. Search for a movie or TV show
3. Browse the results - items already in your library will be marked
4. Click on items to see more details, stream, or download

## Development

Want to build it yourself or contribute?

```bash
git clone https://github.com/enach/jellyfin-plugin-jellyscout.git
cd jellyfin-plugin-jellyscout
dotnet restore
dotnet build --configuration Release
```

The built files will be in `bin/Release/net8.0/`.

## Issues and help

If something isn't working or you have questions:

- Check the [GitHub Issues](https://github.com/enach/jellyfin-plugin-jellyscout/issues) to see if it's a known problem
- Open a new issue if you found a bug
- Use [GitHub Discussions](https://github.com/enach/jellyfin-plugin-jellyscout/discussions) for questions

## License

MIT License - see the [LICENSE](LICENSE) file for details.

## Thanks

- [Jellyfin](https://jellyfin.org/) for the great media server
- [TMDB](https://www.themoviedb.org/) for the movie database
- [Sonarr](https://sonarr.tv/) and [Radarr](https://radarr.video/) for the download management

---

This is a hobby project, so please be patient with updates and fixes. Pull requests are welcome! 