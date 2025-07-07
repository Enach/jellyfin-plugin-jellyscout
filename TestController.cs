using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout.Controllers;

/// <summary>
/// Test controller for validating plugin functionality.
/// </summary>
[ApiController]
[Route("jellyscout/test")]
[AllowAnonymous]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Run comprehensive plugin tests.
    /// </summary>
    /// <returns>Test results.</returns>
    [HttpGet]
    public IActionResult RunTests()
    {
        var results = new List<TestResult>();

        try
        {
            // Test 1: Plugin Instance
            results.Add(TestPluginInstance());

            // Test 2: Plugin Pages
            results.Add(TestPluginPages());

            // Test 3: Embedded Resources
            results.Add(TestEmbeddedResources());

            // Test 4: Image Resource
            results.Add(TestImageResource());

            // Test 5: Configuration
            results.Add(TestConfiguration());

            // Test 6: Controllers
            results.Add(TestControllers());

            var summary = new
            {
                TotalTests = results.Count,
                Passed = results.Count(r => r.Passed),
                Failed = results.Count(r => !r.Passed),
                Results = results,
                PluginInfo = GetPluginInfoSummary()
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running plugin tests");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test specific functionality.
    /// </summary>
    /// <param name="testName">The test to run.</param>
    /// <returns>Test result.</returns>
    [HttpGet("{testName}")]
    public IActionResult RunSpecificTest(string testName)
    {
        try
        {
            TestResult result = testName.ToLowerInvariant() switch
            {
                "plugin" => TestPluginInstance(),
                "pages" => TestPluginPages(),
                "resources" => TestEmbeddedResources(),
                "image" => TestImageResource(),
                "config" => TestConfiguration(),
                "controllers" => TestControllers(),
                _ => new TestResult("Unknown Test", false, $"Test '{testName}' not found")
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running specific test: {TestName}", testName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get plugin info for debugging.
    /// </summary>
    /// <returns>Plugin information.</returns>
    [HttpGet("info")]
    public IActionResult GetPluginInfo()
    {
        try
        {
            var plugin = Plugin.Instance;
            var assembly = Assembly.GetExecutingAssembly();
            
            var info = new
            {
                PluginName = plugin?.Name ?? "Not Available",
                PluginId = plugin?.Id.ToString() ?? "Not Available",
                PluginVersion = plugin?.Version?.ToString() ?? "Not Available",
                AssemblyVersion = assembly.GetName().Version?.ToString() ?? "Not Available",
                AssemblyLocation = assembly.Location,
                Pages = plugin?.GetPages()?.Select(p => new
                {
                    p.Name,
                    p.EmbeddedResourcePath,
                    p.EnableInMainMenu,
                    p.MenuSection,
                    p.MenuIcon
                }).ToArray() ?? Array.Empty<object>(),
                EmbeddedResources = assembly.GetManifestResourceNames(),
                TestUrls = new
                {
                    ConfigurationPage = "/web/index.html#/configurationpage?name=JellyScout",
                    CatalogPage = "/web/index.html#/configurationpage?name=JellyScoutCatalog",
                    PluginImage = $"/Plugins/{plugin?.Id}/{plugin?.Version}/Image",
                    TestEndpoint = "/jellyscout/test",
                    ApiEndpoint = "/jellyscout/search?query=test"
                }
            };

            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plugin info");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private TestResult TestPluginInstance()
    {
        try
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                return new TestResult("Plugin Instance", false, "Plugin.Instance is null");
            }

            var details = $"Name: {plugin.Name}, ID: {plugin.Id}, Version: {plugin.Version}";
            return new TestResult("Plugin Instance", true, details);
        }
        catch (Exception ex)
        {
            return new TestResult("Plugin Instance", false, ex.Message);
        }
    }

    private TestResult TestPluginPages()
    {
        try
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                return new TestResult("Plugin Pages", false, "Plugin instance not available");
            }

            var pages = plugin.GetPages()?.ToArray();
            if (pages == null || pages.Length == 0)
            {
                return new TestResult("Plugin Pages", false, "No pages found");
            }

            var pageDetails = pages.Select(p => $"{p.Name} ({p.EmbeddedResourcePath})").ToArray();
            var details = $"Found {pages.Length} pages: {string.Join(", ", pageDetails)}";
            
            // Check for required pages
            var hasConfig = pages.Any(p => p.Name == "JellyScout");
            var hasCatalog = pages.Any(p => p.Name == "JellyScoutCatalog");
            
            if (!hasConfig || !hasCatalog)
            {
                return new TestResult("Plugin Pages", false, $"Missing required pages. Config: {hasConfig}, Catalog: {hasCatalog}. {details}");
            }

            return new TestResult("Plugin Pages", true, details);
        }
        catch (Exception ex)
        {
            return new TestResult("Plugin Pages", false, ex.Message);
        }
    }

    private TestResult TestEmbeddedResources()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames();
            
            var webResources = resources.Where(r => r.Contains(".Web.")).ToArray();
            var hasConfigPage = webResources.Any(r => r.Contains("ConfigurationPage.html"));
            var hasCatalogPage = webResources.Any(r => r.Contains("CatalogPage.html"));
            
            var details = $"Total resources: {resources.Length}, Web resources: {webResources.Length}";
            
            if (!hasConfigPage || !hasCatalogPage)
            {
                return new TestResult("Embedded Resources", false, $"Missing web pages. Config: {hasConfigPage}, Catalog: {hasCatalogPage}. {details}");
            }

            return new TestResult("Embedded Resources", true, details);
        }
        catch (Exception ex)
        {
            return new TestResult("Embedded Resources", false, ex.Message);
        }
    }

    private TestResult TestImageResource()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames();
            
            var iconResource = resources.FirstOrDefault(r => r.Contains("icon.png"));
            if (iconResource == null)
            {
                return new TestResult("Image Resource", false, "icon.png not found in embedded resources");
            }

            using var stream = assembly.GetManifestResourceStream(iconResource);
            if (stream == null)
            {
                return new TestResult("Image Resource", false, "Could not open icon.png stream");
            }

            var size = stream.Length;
            return new TestResult("Image Resource", true, $"Icon found: {iconResource}, Size: {size} bytes");
        }
        catch (Exception ex)
        {
            return new TestResult("Image Resource", false, ex.Message);
        }
    }

    private TestResult TestConfiguration()
    {
        try
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                return new TestResult("Configuration", false, "Plugin instance not available");
            }

            var config = plugin.Configuration;
            if (config == null)
            {
                return new TestResult("Configuration", false, "Configuration is null");
            }

            var details = $"Config type: {config.GetType().Name}";
            return new TestResult("Configuration", true, details);
        }
        catch (Exception ex)
        {
            return new TestResult("Configuration", false, ex.Message);
        }
    }

    private TestResult TestControllers()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var controllers = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Controller"))
                .Select(t => t.Name)
                .ToArray();

            var details = $"Found controllers: {string.Join(", ", controllers)}";
            return new TestResult("Controllers", true, details);
        }
        catch (Exception ex)
        {
            return new TestResult("Controllers", false, ex.Message);
        }
    }

    private object GetPluginInfoSummary()
    {
        var plugin = Plugin.Instance;
        return new
        {
            Name = plugin?.Name,
            Id = plugin?.Id.ToString(),
            Version = plugin?.Version?.ToString(),
            Description = plugin?.Description
        };
    }
}

/// <summary>
/// Test result model.
/// </summary>
public class TestResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestResult"/> class.
    /// </summary>
    /// <param name="name">Test name.</param>
    /// <param name="passed">Whether the test passed.</param>
    /// <param name="details">Test details.</param>
    public TestResult(string name, bool passed, string details)
    {
        Name = name;
        Passed = passed;
        Details = details;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the test name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the test passed.
    /// </summary>
    public bool Passed { get; }

    /// <summary>
    /// Gets the test details.
    /// </summary>
    public string Details { get; }

    /// <summary>
    /// Gets the test timestamp.
    /// </summary>
    public DateTime Timestamp { get; }
} 