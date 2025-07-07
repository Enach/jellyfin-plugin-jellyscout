using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyScout.Services;

/// <summary>
/// Service for handling retries and circuit breaker pattern for resilient external API calls.
/// </summary>
public class RetryService
{
    private readonly ILogger<RetryService> _logger;
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public RetryService(ILogger<RetryService> logger)
    {
        _logger = logger;
        _circuitBreakers = new ConcurrentDictionary<string, CircuitBreakerState>();
    }

    /// <summary>
    /// Executes an operation with retry logic and circuit breaker pattern.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="circuitBreakerKey">The circuit breaker key for grouping operations.</param>
    /// <param name="maxRetries">Maximum number of retries.</param>
    /// <param name="baseDelay">Base delay for exponential backoff.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string circuitBreakerKey,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default)
    {
        var delay = baseDelay ?? TimeSpan.FromSeconds(1);
        var circuitBreaker = GetOrCreateCircuitBreaker(circuitBreakerKey);

        // Check circuit breaker state
        if (circuitBreaker.State == CircuitState.Open)
        {
            if (DateTime.UtcNow < circuitBreaker.NextAttempt)
            {
                _logger.LogWarning("Circuit breaker is open for {Key}. Next attempt at {NextAttempt}", 
                    circuitBreakerKey, circuitBreaker.NextAttempt);
                throw new CircuitBreakerOpenException($"Circuit breaker is open for {circuitBreakerKey}");
            }
            
            // Transition to half-open
            circuitBreaker.State = CircuitState.HalfOpen;
            _logger.LogInformation("Circuit breaker transitioning to half-open for {Key}", circuitBreakerKey);
        }

        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= maxRetries)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await operation();
                
                // Success - reset circuit breaker
                if (circuitBreaker.State == CircuitState.HalfOpen || circuitBreaker.FailureCount > 0)
                {
                    circuitBreaker.Reset();
                    _logger.LogDebug("Circuit breaker reset for {Key} after successful operation", circuitBreakerKey);
                }

                return result;
            }
            catch (Exception ex) when (ShouldRetry(ex, attempt, maxRetries))
            {
                lastException = ex;
                attempt++;
                
                // Record failure
                circuitBreaker.RecordFailure();
                
                if (attempt <= maxRetries)
                {
                    var delayMs = CalculateDelay(delay, attempt);
                    _logger.LogWarning("Operation failed (attempt {Attempt}/{MaxRetries}) for {Key}. Retrying in {Delay}ms. Error: {Error}",
                        attempt, maxRetries + 1, circuitBreakerKey, delayMs, ex.Message);
                    
                    try
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                // Non-retryable exception
                circuitBreaker.RecordFailure();
                _logger.LogError(ex, "Non-retryable error occurred for {Key}", circuitBreakerKey);
                throw;
            }
        }

        // Check if circuit breaker should open
        if (circuitBreaker.ShouldOpen())
        {
            circuitBreaker.Open();
            _logger.LogError("Circuit breaker opened for {Key} after {Failures} failures", 
                circuitBreakerKey, circuitBreaker.FailureCount);
        }

        _logger.LogError("Operation failed after {Attempts} attempts for {Key}", attempt, circuitBreakerKey);
        throw lastException ?? new InvalidOperationException("Operation failed after maximum retries");
    }

    /// <summary>
    /// Executes an HTTP operation with retry logic and circuit breaker pattern.
    /// </summary>
    /// <param name="httpOperation">The HTTP operation to execute.</param>
    /// <param name="circuitBreakerKey">The circuit breaker key.</param>
    /// <param name="maxRetries">Maximum number of retries.</param>
    /// <param name="baseDelay">Base delay for exponential backoff.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    public async Task<HttpResponseMessage> ExecuteHttpAsync(
        Func<Task<HttpResponseMessage>> httpOperation,
        string circuitBreakerKey,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var response = await httpOperation();
            
            // Check if response indicates a retryable error
            if (!response.IsSuccessStatusCode && IsRetryableHttpStatus(response.StatusCode))
            {
                throw new HttpRequestException($"HTTP {response.StatusCode}: {response.ReasonPhrase}");
            }
            
            return response;
        }, circuitBreakerKey, maxRetries, baseDelay, cancellationToken);
    }

    /// <summary>
    /// Gets circuit breaker statistics for a specific key.
    /// </summary>
    /// <param name="circuitBreakerKey">The circuit breaker key.</param>
    /// <returns>Circuit breaker statistics.</returns>
    public CircuitBreakerStats GetCircuitBreakerStats(string circuitBreakerKey)
    {
        if (_circuitBreakers.TryGetValue(circuitBreakerKey, out var breaker))
        {
            return new CircuitBreakerStats
            {
                Key = circuitBreakerKey,
                State = breaker.State,
                FailureCount = breaker.FailureCount,
                LastFailure = breaker.LastFailure,
                NextAttempt = breaker.NextAttempt
            };
        }

        return new CircuitBreakerStats
        {
            Key = circuitBreakerKey,
            State = CircuitState.Closed,
            FailureCount = 0
        };
    }

    /// <summary>
    /// Resets all circuit breakers.
    /// </summary>
    public void ResetAllCircuitBreakers()
    {
        foreach (var breaker in _circuitBreakers.Values)
        {
            breaker.Reset();
        }
        _logger.LogInformation("All circuit breakers have been reset");
    }

    private CircuitBreakerState GetOrCreateCircuitBreaker(string key)
    {
        return _circuitBreakers.GetOrAdd(key, _ => new CircuitBreakerState());
    }

    private bool ShouldRetry(Exception exception, int attempt, int maxRetries)
    {
        if (attempt >= maxRetries)
            return false;

        return exception switch
        {
            HttpRequestException httpEx => IsRetryableHttpException(httpEx),
            TaskCanceledException => true, // Timeout
            OperationCanceledException => false, // User cancellation
            _ => false // Don't retry other exceptions by default
        };
    }

    private bool IsRetryableHttpException(HttpRequestException httpException)
    {
        // Retry on network-related errors or specific HTTP status codes
        var message = httpException.Message.ToLowerInvariant();
        return message.Contains("timeout") ||
               message.Contains("connection") ||
               message.Contains("network") ||
               message.Contains("dns") ||
               message.Contains("503") ||
               message.Contains("502") ||
               message.Contains("504");
    }

    private bool IsRetryableHttpStatus(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.RequestTimeout => true,
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }

    private int CalculateDelay(TimeSpan baseDelay, int attempt)
    {
        // Exponential backoff with jitter
        var exponentialDelay = baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var jitter = new Random().NextDouble() * 0.1; // 10% jitter
        return (int)(exponentialDelay * (1 + jitter));
    }
}

/// <summary>
/// Circuit breaker state management.
/// </summary>
internal class CircuitBreakerState
{
    private const int FailureThreshold = 5;
    private const int TimeoutMinutes = 5;

    public CircuitState State { get; set; } = CircuitState.Closed;
    public int FailureCount { get; private set; }
    public DateTime LastFailure { get; private set; }
    public DateTime NextAttempt { get; private set; }

    public void RecordFailure()
    {
        FailureCount++;
        LastFailure = DateTime.UtcNow;
    }

    public bool ShouldOpen()
    {
        return State == CircuitState.Closed && FailureCount >= FailureThreshold;
    }

    public void Open()
    {
        State = CircuitState.Open;
        NextAttempt = DateTime.UtcNow.AddMinutes(TimeoutMinutes);
    }

    public void Reset()
    {
        State = CircuitState.Closed;
        FailureCount = 0;
        LastFailure = default;
        NextAttempt = default;
    }
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed - normal operation.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open - blocking requests.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open - testing if service has recovered.
    /// </summary>
    HalfOpen
}

/// <summary>
/// Circuit breaker statistics.
/// </summary>
public class CircuitBreakerStats
{
    /// <summary>
    /// Gets or sets the circuit breaker key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the circuit state.
    /// </summary>
    public CircuitState State { get; set; }

    /// <summary>
    /// Gets or sets the failure count.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the last failure time.
    /// </summary>
    public DateTime? LastFailure { get; set; }

    /// <summary>
    /// Gets or sets the next attempt time.
    /// </summary>
    public DateTime? NextAttempt { get; set; }
}

/// <summary>
/// Exception thrown when circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public CircuitBreakerOpenException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException)
    {
    }
} 