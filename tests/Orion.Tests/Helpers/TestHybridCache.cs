using Microsoft.Extensions.Caching.Hybrid;

namespace Orion.Tests.Helpers;

internal sealed class TestHybridCache : HybridCache
{
    private readonly Dictionary<string, object?> _store = new();

    public int FactoryCallCount { get; private set; }

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var cached))
        {
            return (T)cached!;
        }

        FactoryCallCount++;
        var value = await factory(state, cancellationToken);
        _store[key] = value;
        return value;
    }

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        _store[key] = value;
        return ValueTask.CompletedTask;
    }
}
