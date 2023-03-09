using Microsoft.Extensions.Caching.Memory;

using TaskCaching.Internals;


namespace TaskCaching;

public static class MemoryCacheExtensions
{
    public static async Task<T> GetOrCreateTask<T>(this IMemoryCache cache, string key, Func<ICacheEntry, Task<T>> valueFactory, bool expireOnCompletion)
    {
        var asyncLazyValue = cache.GetOrCreate(
            key,
            entry => !expireOnCompletion
                ? new AsyncLazy<T>(() => valueFactory(entry))
                : new AsyncLazy<T>(
                    () => valueFactory(entry)
                        .ContinueWith(task => {
                            //Add an expiration token that triggers when the Task has completed (succeeded/failed/cancelled)
                            entry.AddExpirationToken(new TaskChangeToken(task));
                            return task;
                        })
                        .Unwrap()
                )
        );

        try {
            var result = await asyncLazyValue.ConfigureAwait(false);

            //The awaited Task has completed. Check that the task still is the same version
            //that the cache returns (i.e. the awaited task has not been invalidated during the await).
            if (cache.TryGetValue(key, out var existingValue)) {
                if (existingValue != asyncLazyValue) {
                    // The awaited value is no more the most recent one.
                    // Get the most recent value with a recursive call.
                    return await cache.GetOrCreateTask(key, valueFactory, expireOnCompletion)
                        .ConfigureAwait(false);
                }
            }

            return result;

        } catch (Exception) {
            //Task object for the given key failed with exception. Remove the task from the cache.
            cache.Remove(key);
            //Re-throw the exception to be handled by the caller.
            throw;
        }
    }
}