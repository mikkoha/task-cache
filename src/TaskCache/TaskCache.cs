using Microsoft.Extensions.Caching.Memory;


namespace TaskCaching;

public class TaskCache : ITaskCache
{
    public Task<T> AddOrGetExisting<T>(string key, Func<Task<T>> valueFactory, bool expireOnCompletion = false)
        => _cache.GetOrCreateTask(key, _ => valueFactory(), expireOnCompletion);


    public void Invalidate(string key)
        => _cache.Remove(key);


    public bool Contains(string key)
        => _cache.TryGetValue(key, out _);


    public void Clear()
        => _cache.Clear();


    public void Dispose()
        => _cache?.Dispose();


    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
}
