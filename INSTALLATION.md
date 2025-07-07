# JellyScout Installation Guide

This guide provides step-by-step instructions for installing the JellyScout plugin in your Jellyfin server.

## Prerequisites

- **Jellyfin Server**: Version 10.9.0 or higher
- **Operating System**: Windows, Linux, or macOS
- **Framework**: .NET 8.0 runtime (usually included with Jellyfin)
- **API Keys**: TMDB API key (free registration required)

## Installation Methods

### Method 1: Manual Installation (Recommended)

#### 1. Download the Plugin

**Option A: Download from GitHub Releases**
```bash
# Download the latest release
wget https://github.com/enach/jellyfin-plugin-jellyscout/releases/latest/download/JellyScout-v0.0.2.zip

# Extract the archive
unzip JellyScout-v0.0.2.zip -d jellyscout-plugin/
```

**Option B: Build from Source**
```bash
# Clone the repository
git clone https://github.com/enach/jellyfin-plugin-jellyscout.git
cd jellyfin-plugin-jellyscout

# Build the plugin
dotnet build --configuration Release

# The built files will be in bin/Release/net8.0/
```

#### 2. Find Your Jellyfin Plugin Directory

The plugin directory varies by operating system:

**Windows:**
```
C:\ProgramData\Jellyfin\Server\plugins\
```

**Linux:**
```
/var/lib/jellyfin/plugins/
```

**macOS:**
```
/Users/[username]/.local/share/jellyfin/plugins/
```

**Docker:**
```
/config/plugins/
```

#### 3. Install the Plugin

1. Create a directory for the plugin:
   ```bash
   mkdir -p "[JELLYFIN_DATA]/plugins/JellyScout"
   ```

2. Copy the plugin files:
   ```bash
   # Copy all DLL files and manifest
   cp *.dll "[JELLYFIN_DATA]/plugins/JellyScout/"
   cp manifest.json "[JELLYFIN_DATA]/plugins/JellyScout/"
   ```

3. Set proper permissions (Linux/macOS):
   ```bash
   chown -R jellyfin:jellyfin "[JELLYFIN_DATA]/plugins/JellyScout/"
   chmod -R 755 "[JELLYFIN_DATA]/plugins/JellyScout/"
   ```

#### 4. Restart Jellyfin

**Windows Service:**
```cmd
net stop Jellyfin
net start Jellyfin
```

**Linux Systemd:**
```bash
sudo systemctl restart jellyfin
```

**Docker:**
```bash
docker restart jellyfin
```

### Method 2: Plugin Repository (Future)

> **Note**: This method will be available once the plugin is added to the official Jellyfin plugin catalog.

1. Open Jellyfin Admin Dashboard
2. Go to **Plugins** → **Catalog**
3. Search for "JellyScout"
4. Click **Install**
5. Restart Jellyfin when prompted

## Configuration

### 1. Access Plugin Settings

1. Open Jellyfin Admin Dashboard
2. Navigate to **Plugins** → **My Plugins**
3. Find **JellyScout** in the list
4. Click **Settings**

### 2. Configure TMDB API

1. **Get TMDB API Key:**
   - Visit [TMDB API Settings](https://www.themoviedb.org/settings/api)
   - Create a free account if needed
   - Request an API key (choose "Developer" option)
   - Copy your API key

2. **Enter API Key:**
   - In JellyScout settings, paste your TMDB API key
   - Click **Save**

### 3. Configure External Services (Optional)

**Streamio Integration:**
- Server URL: `http://your-streamio-server:port`
- API Key: Your Streamio API key
- Timeout: 30 seconds (default)

**Sonarr Integration (TV Shows):**
- Server URL: `http://your-sonarr-server:8989`
- API Key: Your Sonarr API key
- Quality Profile: Your preferred quality profile

**Radarr Integration (Movies):**
- Server URL: `http://your-radarr-server:7878`
- API Key: Your Radarr API key
- Quality Profile: Your preferred quality profile

### 4. Test Configuration

1. Save all settings
2. Click **Test Connection** for each service
3. Verify all connections are successful

## Usage

### Access the Plugin

1. Open Jellyfin web interface
2. Navigate to **JellyScout** from the main menu
3. Start searching for movies and TV shows

### Basic Operations

- **Search**: Enter movie/TV show name and press Enter
- **View Details**: Click "Details" button for full information
- **Stream**: Click "Stream" to start streaming
- **Download**: Click "Download" to download content
- **Library Check**: Items in your library are automatically marked

## Troubleshooting

### Plugin Not Appearing

1. **Check Installation:**
   ```bash
   ls -la "[JELLYFIN_DATA]/plugins/JellyScout/"
   ```

2. **Verify Permissions:**
   ```bash
   chown -R jellyfin:jellyfin "[JELLYFIN_DATA]/plugins/JellyScout/"
   ```

3. **Check Jellyfin Logs:**
   ```bash
   tail -f "[JELLYFIN_DATA]/logs/jellyfin.log"
   ```

### API Connection Issues

1. **TMDB API:**
   - Verify API key is correct
   - Check internet connection
   - Ensure API key has proper permissions

2. **External Services:**
   - Verify server URLs are reachable
   - Check API keys are valid
   - Ensure services are running

### Plugin Loading Errors

1. **Check Framework Version:**
   ```bash
   dotnet --version
   ```

2. **Verify Dependencies:**
   - Ensure all DLL files are present
   - Check manifest.json is valid

3. **Review Logs:**
   - Check Jellyfin logs for error messages
   - Look for plugin-specific errors

## Uninstallation

1. **Stop Jellyfin:**
   ```bash
   sudo systemctl stop jellyfin
   ```

2. **Remove Plugin Directory:**
   ```bash
   rm -rf "[JELLYFIN_DATA]/plugins/JellyScout/"
   ```

3. **Restart Jellyfin:**
   ```bash
   sudo systemctl start jellyfin
   ```

## Support

- **Issues**: [GitHub Issues](https://github.com/enach/jellyfin-plugin-jellyscout/issues)
- **Discussions**: [GitHub Discussions](https://github.com/enach/jellyfin-plugin-jellyscout/discussions)
- **Wiki**: [Plugin Wiki](https://github.com/enach/jellyfin-plugin-jellyscout/wiki)

## Directory Structure

After installation, your plugin directory should look like:

```
[JELLYFIN_DATA]/plugins/JellyScout/
├── Jellyfin.Plugin.JellyScout.dll
├── manifest.json
└── [other dependency DLLs]
```

## Security Notes

- Store API keys securely
- Use HTTPS for external service connections
- Regularly update the plugin for security patches
- Monitor plugin logs for suspicious activity 