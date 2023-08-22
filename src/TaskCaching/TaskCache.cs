using Microsoft.Extensions.Caching.Memory;


namespace TaskCaching;

/// <summary>
/// A class that implements the <see cref="ITaskCache"/> interface using an internal instance of <see cref="MemoryCache"/>.
/// </summary>
public class TaskCache : ITaskCache
{
    /// <inheritdoc />
    public Task<T> AddOrGetExisting<T>(string key, Func<Task<T>> valueFactory, bool expireOnCompletion = false)
        => _cache.GetOrCreateTask(key, _ => valueFactory(), expireOnCompletion);


    /// <inheritdoc />
    public void Invalidate(string key)
        => _cache.Remove(key);


    /// <inheritdoc />
    public bool Contains(string key)
        => _cache.TryGetValue(key, out _);


    /// <inheritdoc />
    public void Clear()
        => _cache.Clear();


    /// <inheritdoc />
    public void Dispose()
        => _cache?.Dispose();


    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
}
