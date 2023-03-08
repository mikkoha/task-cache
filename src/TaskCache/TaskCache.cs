using Microsoft.Extensions.Caching.Memory;


namespace TaskCaching;

public class TaskCache : ITaskCache, IDisposable
{
    public Task<T> AddOrGetExisting<T>(string key, Func<Task<T>> valueFactory, MemoryCacheEntryOptions options = default)
        => AddOrGetExistingCore(key, valueFactory, options, false);


    public Task<T> AddOrGetExisting<T>(string key, Func<Task<T>> valueFactory, TaskCacheEntryOptions options)
        => AddOrGetExistingCore(key, valueFactory, options, options?.ExpirationOnCompletion ?? false);


    private async Task<T> AddOrGetExistingCore<T>(string key, Func<Task<T>> valueFactory, MemoryCacheEntryOptions options, bool expirationOnCompletion)
    {
        var asyncLazyValue = _cache.GetOrCreate(key, entry => {
            if (options == null) {
                return new AsyncLazy<T>(valueFactory);
            }

            //Customize expiration as per the policy
            entry.SetOptions(options);
            if (expirationOnCompletion) {
                return new AsyncLazy<T>(
                    () => valueFactory()
                        .ContinueWith(task => {
                            //Add an expiration token which triggers when the Task has completed (succeeded/failed/cancelled)
                            entry.AddExpirationToken(new TaskChangeToken(task));
                            return task;
                        })
                        .Unwrap()
                );
            }

            return new AsyncLazy<T>(valueFactory);
        });

        try {
            var result = await asyncLazyValue.ConfigureAwait(false);

            //The awaited Task has completed. Check that the task still is the same version
            //that the cache returns (i.e. the awaited task has not been invalidated during the await).
            if (_cache.TryGetValue(key, out var existingValue)) {
                if (existingValue != asyncLazyValue) {
                    // The awaited value is no more the most recent one.
                    // Get the most recent value with a recursive call.
                    return await AddOrGetExistingCore(key, valueFactory, options, expirationOnCompletion)
                        .ConfigureAwait(false);
                }
            }

            return result;

        } catch (Exception) {
            //Task object for the given key failed with exception. Remove the task from the cache.
            _cache.Remove(key);
            //Re-throw the exception to be handled by the caller.
            throw;
        }
    }


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