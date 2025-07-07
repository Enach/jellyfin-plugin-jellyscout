using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout.Controllers;

/// <summary>
/// Controller for serving plugin images.
/// </summary>
[ApiController]
[Route("Plugins")]
public class PluginImageController : ControllerBase
{
    private readonly ILogger<PluginImageController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginImageController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PluginImageController(ILogger<PluginImageController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the plugin image.
    /// </summary>
    /// <param name="guid">The plugin GUID.</param>
    /// <param name="version">The plugin version.</param>
    /// <returns>The plugin image.</returns>
    [HttpGet("{guid}/{version}/Image")]
    [AllowAnonymous]
    public IActionResult GetPluginImage(string guid, string version)
    {
        try
        {
            // Verify this is our plugin's GUID
            if (!string.Equals(guid, "a8b4d8c6-3f2a-4d7e-9b1c-5e8f2a9d3c7b", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            // Get the embedded icon resource
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Jellyfin.Plugin.JellyScout.icon.png";
            
            _logger.LogDebug("Looking for resource: {ResourceName}", resourceName);
            _logger.LogDebug("Available resources: {Resources}", string.Join(", ", assembly.GetManifestResourceNames()));
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                _logger.LogWarning("Plugin icon resource not found: {ResourceName}", resourceName);
                return NotFound();
            }

            // Read the stream into a byte array
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var imageBytes = memoryStream.ToArray();

            return File(imageBytes, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving plugin image for GUID: {Guid}, Version: {Version}", guid, version);
            return StatusCode(500);
        }
    }
} 