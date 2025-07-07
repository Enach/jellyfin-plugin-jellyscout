// JellyScout JavaScript - Enhanced version with modern features

class JellyScout {
    constructor() {
        this.connection = null;
        this.isSearching = false;
        this.currentQuery = '';
        this.init();
    }

    async init() {
        this.bindEvents();
        await this.setupSignalR();
        await this.loadStatus();
        this.showNotification('JellyScout loaded successfully!', 'info');
    }

    bindEvents() {
        const searchForm = document.getElementById('searchForm');
        const searchInput = document.getElementById('searchInput');

        searchForm.addEventListener('submit', (e) => {
            e.preventDefault();
            this.performSearch();
        });

        searchInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                this.performSearch();
            }
        });

        // Auto-search on input with debounce
        let searchTimeout;
        searchInput.addEventListener('input', (e) => {
            clearTimeout(searchTimeout);
            const query = e.target.value.trim();
            
            if (query.length >= 3) {
                searchTimeout = setTimeout(() => {
                    this.performSearch();
                }, 500);
            } else if (query.length === 0) {
                this.clearResults();
            }
        });
    }

    async setupSignalR() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/jellyscout/notifications")
                .withAutomaticReconnect()
                .build();

            // Handle different notification types
            this.connection.on("Notification", (notification) => {
                this.handleNotification(notification);
            });

            this.connection.on("DownloadReady", (data) => {
                this.showNotification(`✅ Download ready: ${data.Title} (${data.Year})`, 'success');
            });

            this.connection.on("StreamingReady", (data) => {
                this.showNotification(`🎬 Streaming ready: ${data.Title} (${data.Year})`, 'success');
                if (data.StreamingUrl) {
                    this.openStreamingUrl(data.StreamingUrl);
                }
            });

            this.connection.on("Error", (data) => {
                this.showNotification(`❌ Error: ${data.Error}`, 'error');
            });

            this.connection.on("Progress", (data) => {
                this.showNotification(`📊 ${data.Title}: ${data.Status} (${data.Progress}%)`, 'info');
            });

            await this.connection.start();
            console.log("SignalR connected successfully");
        } catch (error) {
            console.error("SignalR connection failed:", error);
            this.showNotification("Real-time notifications unavailable", 'error');
        }
    }

    async loadStatus() {
        try {
            const response = await fetch('/jellyscout/status');
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            
            const status = await response.json();
            this.updateStatusBar(status);
        } catch (error) {
            console.error('Failed to load status:', error);
            this.showNotification('Failed to load plugin status', 'error');
        }
    }

    updateStatusBar(status) {
        const configStatus = document.getElementById('configStatus');
        const streamingStatus = document.getElementById('streamingStatus');
        const downloadStatus = document.getElementById('downloadStatus');
        const notificationStatus = document.getElementById('notificationStatus');

        configStatus.className = `status-dot ${status.configured ? 'active' : 'inactive'}`;
        streamingStatus.className = `status-dot ${status.streamingEnabled ? 'active' : 'inactive'}`;
        downloadStatus.className = `status-dot ${status.downloadsEnabled ? 'active' : 'inactive'}`;
        notificationStatus.className = `status-dot ${status.notificationsEnabled ? 'active' : 'inactive'}`;

        if (!status.configured) {
            this.showNotification('Plugin not configured. Please add your TMDB API key.', 'error');
        }
    }

    async performSearch() {
        const query = document.getElementById('searchInput').value.trim();
        
        if (!query) {
            this.showNotification('Please enter a search term', 'error');
            return;
        }

        if (this.isSearching) {
            return;
        }

        this.isSearching = true;
        this.currentQuery = query;
        this.showLoading(true);
        this.clearResults();

        try {
            const response = await fetch(`/jellyscout/search?query=${encodeURIComponent(query)}&maxResults=20`);
            
            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || `HTTP ${response.status}`);
            }

            const data = await response.json();
            this.displayResults(data.results);
            
            if (data.results.length === 0) {
                this.showNoResults();
            }
        } catch (error) {
            console.error('Search failed:', error);
            this.showNotification(`Search failed: ${error.message}`, 'error');
            this.showNoResults();
        } finally {
            this.isSearching = false;
            this.showLoading(false);
        }
    }

    displayResults(results) {
        const container = document.getElementById('resultsContainer');
        container.innerHTML = '';

        results.forEach(item => {
            const card = this.createResultCard(item);
            container.appendChild(card);
            
            // Check download status for each item
            this.checkDownloadStatus(item.tmdbId, item.mediaType || 'movie');
        });
    }

    createResultCard(item) {
        const card = document.createElement('div');
        card.className = `result-card ${item.alreadyInLibrary ? 'in-library' : ''}`;
        card.dataset.tmdbId = item.tmdbId;
        card.dataset.mediaType = item.mediaType || 'movie';

        const title = item.title || 'Unknown Title';
        const year = item.year || 'Unknown';
        const mediaType = item.mediaType || 'unknown';
        const overview = item.overview || 'No description available.';
        const rating = item.voteAverage ? `⭐ ${item.voteAverage.toFixed(1)}` : '';

        card.innerHTML = `
            ${item.alreadyInLibrary ? '<div class="library-status">✅ In Library</div>' : ''}
            <div class="result-header">
                <div>
                    <div class="result-title">${this.escapeHtml(title)}</div>
                    <div class="result-meta">
                        <span>${year}</span>
                        <span>${mediaType.toUpperCase()}</span>
                        ${rating ? `<span>${rating}</span>` : ''}
                    </div>
                </div>
            </div>
            <div class="result-overview">${this.escapeHtml(overview)}</div>
            <div class="download-status" id="status-${item.tmdbId}">
                <div class="status-loading">Checking status...</div>
            </div>
            <div class="result-actions">
                <button class="action-btn details-btn" onclick="jellyScout.showDetails(${item.tmdbId}, '${mediaType}')">
                    Details
                </button>
                ${mediaType === 'tv' && !item.alreadyInLibrary ? `
                <button class="action-btn sonarr-btn" onclick="jellyScout.addToSonarr('${this.escapeHtml(title)}', ${item.tmdbId}, ${item.year})">
                    Add to Sonarr
                </button>` : ''}
                ${mediaType === 'movie' && !item.alreadyInLibrary ? `
                <button class="action-btn radarr-btn" onclick="jellyScout.addToRadarr('${this.escapeHtml(title)}', ${item.tmdbId}, ${item.year})">
                    Add to Radarr
                </button>` : ''}
                <button class="action-btn stream-btn" onclick="jellyScout.searchTorrents('${this.escapeHtml(title)}', ${item.year}, '${mediaType}')" 
                        ${item.alreadyInLibrary ? 'disabled' : ''}>
                    Stream
                </button>
                <button class="action-btn download-btn" onclick="jellyScout.searchTorrents('${this.escapeHtml(title)}', ${item.year}, '${mediaType}', true)" 
                        ${item.alreadyInLibrary ? 'disabled' : ''}>
                    Download
                </button>
            </div>
        `;

        return card;
    }

    async showDetails(tmdbId, mediaType) {
        try {
            const response = await fetch(`/jellyscout/details/${tmdbId}?mediaType=${mediaType}`);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const details = await response.json();
            this.displayDetailsModal(details);
        } catch (error) {
            console.error('Failed to load details:', error);
            this.showNotification('Failed to load details', 'error');
        }
    }

    displayDetailsModal(details) {
        // Create a simple modal (you could enhance this with a proper modal library)
        const modal = document.createElement('div');
        modal.style.cssText = `
            position: fixed; top: 0; left: 0; width: 100%; height: 100%; 
            background: rgba(0,0,0,0.8); z-index: 2000; display: flex; 
            align-items: center; justify-content: center; padding: 20px;
        `;

        const content = document.createElement('div');
        content.style.cssText = `
            background: white; color: #333; padding: 30px; border-radius: 12px; 
            max-width: 600px; max-height: 80vh; overflow-y: auto; position: relative;
        `;

        const title = details.title || details.name || 'Unknown';
        const overview = details.overview || 'No description available.';
        const releaseDate = details.release_date || details.first_air_date || 'Unknown';
        const rating = details.vote_average ? `⭐ ${details.vote_average.toFixed(1)}/10` : 'No rating';
        const runtime = details.runtime ? `${details.runtime} minutes` : '';
        const genres = details.genres ? details.genres.map(g => g.name).join(', ') : 'Unknown';

        content.innerHTML = `
            <div style="position: absolute; top: 10px; right: 15px; cursor: pointer; font-size: 24px;" onclick="this.parentElement.parentElement.remove()">×</div>
            <h2 style="margin-bottom: 15px;">${this.escapeHtml(title)}</h2>
            <p style="margin-bottom: 10px;"><strong>Release Date:</strong> ${releaseDate}</p>
            <p style="margin-bottom: 10px;"><strong>Rating:</strong> ${rating}</p>
            <p style="margin-bottom: 10px;"><strong>Genres:</strong> ${genres}</p>
            ${runtime ? `<p style="margin-bottom: 10px;"><strong>Runtime:</strong> ${runtime}</p>` : ''}
            <p style="margin-bottom: 15px;"><strong>Overview:</strong></p>
            <p style="line-height: 1.6;">${this.escapeHtml(overview)}</p>
        `;

        modal.appendChild(content);
        document.body.appendChild(modal);

        // Close on background click
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                modal.remove();
            }
        });
    }

    async searchTorrents(title, year, mediaType, isDownload = false) {
        try {
            this.showNotification(`Searching for torrents: ${title}`, 'info');
            
            const response = await fetch(`/jellyscout/torrents?title=${encodeURIComponent(title)}&year=${year}&mediaType=${mediaType}`);
            
            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || `HTTP ${response.status}`);
            }

            const data = await response.json();
            
            if (data.torrents.length === 0) {
                this.showNotification('No torrents found for this title', 'error');
                return;
            }

            // For demo purposes, we'll simulate selecting the first torrent
            const torrent = data.torrents[0];
            
            if (isDownload) {
                await this.startDownload(torrent.magnetLink, title);
            } else {
                await this.startStreaming(torrent.magnetLink, title);
            }
        } catch (error) {
            console.error('Torrent search failed:', error);
            this.showNotification(`Torrent search failed: ${error.message}`, 'error');
        }
    }

    async startStreaming(magnetLink, title) {
        try {
            this.showNotification(`Starting stream: ${title}`, 'info');
            
            const response = await fetch('/jellyscout/stream', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    magnetLink: magnetLink,
                    title: title
                })
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || `HTTP ${response.status}`);
            }

            const data = await response.json();
            this.showNotification(`Stream started: ${title}`, 'success');
            
            if (data.streamingUrl) {
                this.openStreamingUrl(data.streamingUrl);
            }
        } catch (error) {
            console.error('Streaming failed:', error);
            this.showNotification(`Streaming failed: ${error.message}`, 'error');
        }
    }

    async startDownload(magnetLink, title) {
        try {
            this.showNotification(`Starting download: ${title}`, 'info');
            
            const response = await fetch('/jellyscout/download', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    magnetLink: magnetLink,
                    title: title
                })
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || `HTTP ${response.status}`);
            }

            const data = await response.json();
            this.showNotification(`Download started: ${title}`, 'success');
        } catch (error) {
            console.error('Download failed:', error);
            this.showNotification(`Download failed: ${error.message}`, 'error');
        }
    }

    async addToSonarr(title, tmdbId, year) {
        try {
            this.showNotification(`Adding to Sonarr: ${title}`, 'info');
            
            const response = await fetch('/jellyscout/sonarr/add', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    title: title,
                    tmdbId: tmdbId,
                    year: year
                })
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || `HTTP ${response.status}`);
            }

            const data = await response.json();
            this.showNotification(`Added to Sonarr: ${title}`, 'success');
            
            // Refresh the status after adding
            setTimeout(() => {
                this.checkDownloadStatus(tmdbId, 'tv');
            }, 1000);
        } catch (error) {
            console.error('Failed to add to Sonarr:', error);
            this.showNotification(`Failed to add to Sonarr: ${error.message}`, 'error');
        }
    }

    async addToRadarr(title, tmdbId, year) {
        try {
            this.showNotification(`Adding to Radarr: ${title}`, 'info');
            
            const response = await fetch('/jellyscout/radarr/add', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    title: title,
                    tmdbId: tmdbId,
                    year: year
                })
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || `HTTP ${response.status}`);
            }

            const data = await response.json();
            this.showNotification(`Added to Radarr: ${title}`, 'success');
            
            // Refresh the status after adding
            setTimeout(() => {
                this.checkDownloadStatus(tmdbId, 'movie');
            }, 1000);
        } catch (error) {
            console.error('Failed to add to Radarr:', error);
            this.showNotification(`Failed to add to Radarr: ${error.message}`, 'error');
        }
    }

    async checkDownloadStatus(tmdbId, mediaType) {
        try {
            const service = mediaType === 'tv' ? 'sonarr' : 'radarr';
            const response = await fetch(`/jellyscout/${service}/status/${tmdbId}`);
            
            if (!response.ok) {
                // If status check fails, just hide the loading indicator
                this.updateStatusDisplay(tmdbId, { status: 'NotInSystem', message: '' });
                return;
            }

            const status = await response.json();
            this.updateStatusDisplay(tmdbId, status);
        } catch (error) {
            console.error('Failed to check download status:', error);
            // Hide loading indicator on error
            this.updateStatusDisplay(tmdbId, { status: 'NotInSystem', message: '' });
        }
    }

    updateStatusDisplay(tmdbId, status) {
        const statusElement = document.getElementById(`status-${tmdbId}`);
        if (!statusElement) return;

        let statusClass = 'status-not-in-system';
        let statusText = '';
        let statusIcon = '';

        switch (status.status) {
            case 'NotInSystem':
                statusClass = 'status-not-in-system';
                statusText = '';
                statusIcon = '';
                break;
            case 'Wanted':
                statusClass = 'status-wanted';
                statusText = '⏳ Wanted';
                statusIcon = '⏳';
                break;
            case 'Downloading':
                statusClass = 'status-downloading';
                statusText = `📥 Downloading (${status.progress}%)`;
                statusIcon = '📥';
                break;
            case 'Downloaded':
                statusClass = 'status-downloaded';
                statusText = '✅ Downloaded';
                statusIcon = '✅';
                break;
            case 'PartiallyDownloaded':
                statusClass = 'status-partial';
                statusText = `🔄 Partial (${status.progress}%)`;
                statusIcon = '🔄';
                break;
            case 'Failed':
                statusClass = 'status-failed';
                statusText = '❌ Failed';
                statusIcon = '❌';
                break;
            case 'NotMonitored':
                statusClass = 'status-not-monitored';
                statusText = '⏸️ Not Monitored';
                statusIcon = '⏸️';
                break;
            default:
                statusClass = 'status-unknown';
                statusText = '❓ Unknown';
                statusIcon = '❓';
        }

        if (statusText) {
            statusElement.innerHTML = `
                <div class="status-badge ${statusClass}">
                    <span class="status-icon">${statusIcon}</span>
                    <span class="status-text">${statusText}</span>
                    ${status.message ? `<span class="status-message">${status.message}</span>` : ''}
                </div>
            `;
        } else {
            statusElement.innerHTML = ''; // Hide status if not in system
        }
    }

    openStreamingUrl(url) {
        // Open in a new tab/window
        window.open(url, '_blank');
    }

    showLoading(show) {
        const loading = document.getElementById('loadingIndicator');
        const searchBtn = document.getElementById('searchBtn');
        
        if (show) {
            loading.style.display = 'block';
            searchBtn.disabled = true;
            searchBtn.textContent = 'Searching...';
        } else {
            loading.style.display = 'none';
            searchBtn.disabled = false;
            searchBtn.textContent = 'Search';
        }
    }

    clearResults() {
        const container = document.getElementById('resultsContainer');
        container.innerHTML = '';
    }

    showNoResults() {
        const container = document.getElementById('resultsContainer');
        container.innerHTML = `
            <div class="no-results">
                <h3>No results found</h3>
                <p>Try adjusting your search terms or check your spelling.</p>
            </div>
        `;
    }

    showNotification(message, type = 'info') {
        const notification = document.getElementById('notification');
        notification.textContent = message;
        notification.className = `notification ${type} show`;

        setTimeout(() => {
            notification.classList.remove('show');
        }, 5000);
    }

    handleNotification(notification) {
        const { Type, Message, Data } = notification;
        
        switch (Type) {
            case 'DownloadReady':
                this.showNotification(`✅ ${Message}`, 'success');
                break;
            case 'StreamingReady':
                this.showNotification(`🎬 ${Message}`, 'success');
                if (Data?.StreamingUrl) {
                    this.openStreamingUrl(Data.StreamingUrl);
                }
                break;
            case 'Error':
                this.showNotification(`❌ ${Message}`, 'error');
                break;
            case 'Progress':
                this.showNotification(`📊 ${Message}`, 'info');
                break;
            default:
                this.showNotification(Message, 'info');
        }
    }

    escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

// Initialize the application
const jellyScout = new JellyScout();
