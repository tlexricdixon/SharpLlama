using SharpLlama.Contracts;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SharpLlama.Infrastructure;

namespace SharpLlama.ChatService;

/// <summary>
/// Provides a pooled set of chat service instances (stateful and stateless) to reduce the overhead
/// of repeatedly constructing expensive model contexts.
/// </summary>
/// <remarks>
/// Thread-safe:
/// - Uses <see cref="ConcurrentQueue{T}"/> for pooling.
/// - Uses <see cref="SemaphoreSlim"/> to bound concurrent checkouts and optionally block until an instance is available.
/// Lifecycle:
/// - Services are created lazily when the pool is empty (up to <see cref="_maxPoolSize"/> concurrent usages).
/// - Returned services are re-queued if not disposed.
/// Disposal:
/// - Implements the full dispose pattern with a finalizer to ensure pooled instances and semaphores are released.
/// Reflection:
/// - Uses reflection in <see cref="IsServiceDisposed(object)"/> to detect disposed service instances before re-queuing.
/// </remarks>
public class ChatServicePool : IChatServicePool, IDisposable
{
    private readonly ConcurrentQueue<IStatefulChatService> _statefulServices = new();
    private readonly ConcurrentQueue<IStatelessChatService> _statelessServices = new();
    private readonly IConfiguration _configuration;
    private readonly ILoggerManager _logger;
    private readonly ILLamaWeightManager _weightManager;
    private readonly int _maxPoolSize;
    private readonly SemaphoreSlim _statefulSemaphore;
    private readonly SemaphoreSlim _statelessSemaphore;
    private bool _disposed = false;

    public ChatServicePool(IConfiguration configuration, ILoggerManager logger, ILLamaWeightManager weightManager)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _weightManager = weightManager ?? throw new ArgumentNullException(nameof(weightManager));

        _maxPoolSize = int.TryParse(configuration["ChatService:MaxPoolSize"], out var size) ? size : 10;
        _statefulSemaphore = new SemaphoreSlim(_maxPoolSize, _maxPoolSize);
        _statelessSemaphore = new SemaphoreSlim(_maxPoolSize, _maxPoolSize);

        _logger.LogInfo($"ChatServicePool initialized with max pool size: {_maxPoolSize}");
    }

    public async Task<IStatefulChatService> GetStatefulServiceAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            _logger.LogError("Attempt to get stateful service after pool disposed");
            throw new ObjectDisposedException(nameof(ChatServicePool));
        }

        try
        {
            await _statefulSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Stateful service acquisition canceled");
            throw;
        }
        catch (ObjectDisposedException)
        {
            _logger.LogError("Stateful semaphore disposed during wait");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error waiting for stateful semaphore: {ex}");
            throw;
        }

        try
        {
            if (_statefulServices.TryDequeue(out var service))
            {
                _logger.LogDebug("Reused pooled StatefulChatService");
                return service;
            }

            _logger.LogDebug("Created new StatefulChatService");
            return new StatefulChatService(
                Options.Create(new ModelOptions()), // Replace with actual ModelOptions if available
                Options.Create(new ChatServiceOptions()), // Replace with actual ChatServiceOptions if available
                _logger,
                _weightManager,
                null, // IChatResponseCache? (optional)
                null  // IChatMetrics? (optional)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to obtain stateful service: {ex}");
            // Important: release semaphore since lease failed
            _statefulSemaphore.Release();
            throw;
        }
    }

    public Task ReturnStatefulServiceAsync(IStatefulChatService service)
    {
        // Always release semaphore exactly once per successful WaitAsync
        try
        {
            if (service == null)
            {
                _logger.LogWarning("Null stateful service returned (ignored)");
                return Task.CompletedTask;
            }

            if (_disposed)
            {
                _logger.LogWarning("Pool disposed; disposing returned stateful service");
                SafeDispose(service);
                return Task.CompletedTask;
            }

            try
            {
                if (service is StatefulChatService statefulService && !IsServiceDisposed(statefulService))
                {
                    _statefulServices.Enqueue(service);
                    _logger.LogDebug("Returned StatefulChatService to pool");
                }
                else
                {
                    SafeDispose(service);
                    _logger.LogDebug("Disposed StatefulChatService instead of returning to pool");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing returned stateful service: {ex}");
                SafeDispose(service);
            }

            return Task.CompletedTask;
        }
        finally
        {
            // Release only if pool not yet disposed; semaphore might already be disposed
            try
            {
                if (!_disposed)
                    _statefulSemaphore.Release();
            }
            catch (ObjectDisposedException) { /* swallow */ }
            catch (SemaphoreFullException)
            {
                _logger.LogWarning("Stateful semaphore full on release (double return?)");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error releasing stateful semaphore: {ex}");
            }
        }
    }

    public async Task<IStatelessChatService> GetStatelessServiceAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            _logger.LogError("Attempt to get stateless service after pool disposed");
            throw new ObjectDisposedException(nameof(ChatServicePool));
        }

        try
        {
            await _statelessSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Stateless service acquisition canceled");
            throw;
        }
        catch (ObjectDisposedException)
        {
            _logger.LogError("Stateless semaphore disposed during wait");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error waiting for stateless semaphore: {ex}");
            throw;
        }

        try
        {
            if (_statelessServices.TryDequeue(out var service))
            {
                _logger.LogDebug("Reused pooled StatelessChatService");
                return service;
            }

            _logger.LogDebug("Created new StatelessChatService");
            return new StatelessChatService(_configuration, _logger, _weightManager);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to obtain stateless service: {ex}");
            _statelessSemaphore.Release();
            throw;
        }
    }

    public Task ReturnStatelessServiceAsync(IStatelessChatService service)
    {
        try
        {
            if (service == null)
            {
                _logger.LogWarning("Null stateless service returned (ignored)");
                return Task.CompletedTask;
            }

            if (_disposed)
            {
                _logger.LogWarning("Pool disposed; disposing returned stateless service");
                SafeDispose(service);
                return Task.CompletedTask;
            }

            try
            {
                if (service is StatelessChatService statelessService && !IsServiceDisposed(statelessService))
                {
                    _statelessServices.Enqueue(service);
                    _logger.LogDebug("Returned StatelessChatService to pool");
                }
                else
                {
                    SafeDispose(service);
                    _logger.LogDebug("Disposed StatelessChatService instead of returning to pool");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing returned stateless service: {ex}");
                SafeDispose(service);
            }

            return Task.CompletedTask;
        }
        finally
        {
            try
            {
                if (!_disposed)
                    _statelessSemaphore.Release();
            }
            catch (ObjectDisposedException) { /* ignore */ }
            catch (SemaphoreFullException)
            {
                _logger.LogWarning("Stateless semaphore full on release (double return?)");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error releasing stateless semaphore: {ex}");
            }
        }
    }

    public int AvailableStatefulCount => _statefulServices.Count;
    public int AvailableStatelessCount => _statelessServices.Count;

    private static bool IsServiceDisposed(object service)
    {
        try
        {
            var disposedField = service.GetType().GetField("_disposed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return disposedField?.GetValue(service) as bool? ?? false;
        }
        catch
        {
            // If reflection fails, assume not disposed to avoid premature discard
            return false;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ChatServicePool()
    {
        try
        {
            Dispose(false);
        }
        catch
        {
            // Avoid throwing from finalizer
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            try
            {
                while (_statefulServices.TryDequeue(out var statefulService))
                {
                    SafeDispose(statefulService);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disposing stateful services: {ex}");
            }

            try
            {
                while (_statelessServices.TryDequeue(out var statelessService))
                {
                    SafeDispose(statelessService);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disposing stateless services: {ex}");
            }

            try { _statefulSemaphore?.Dispose(); } catch (Exception ex) { _logger.LogError($"Error disposing stateful semaphore: {ex}"); }
            try { _statelessSemaphore?.Dispose(); } catch (Exception ex) { _logger.LogError($"Error disposing stateless semaphore: {ex}"); }
        }

        _disposed = true;
        _logger.LogInfo("ChatServicePool disposed");
    }

    private void SafeDispose(object obj)
    {
        if (obj is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disposing instance: {ex}");
            }
        }
    }
}