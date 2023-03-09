using TaskCaching.Internals;


namespace Microsoft.Extensions.Caching.Memory;

public static class MemoryCacheExtensions
{
    /// <summary>
    /// <para>Return the <see cref="Task{TResult}"/> value for a given key from the cache. If the key is not already
    /// present in cache, <paramref name="taskFactory"/> will be run first to create the Task and it will be
    /// added to the cache immediately.</para>
    /// 
    /// <para>This method differs from <see cref="CacheExtensions.GetOrCreateAsync{TItem}"/>, which only caches
    /// the result of a Task, after it has successfully completed. Instead, this method caches a Lazy instance
    /// of the Task itself - allowing long-running/expensive asynchronous operations to be shared and avoiding
    /// concurrent duplication of work.</para>
    /// 
    /// <para>Often when a running Task is returned, it is a Task returned by the <paramref name="taskFactory"/>
    /// the caller has given as a parameter, but the returned task might also have a different origin (from
    /// another call elsewhere using the same key and presumably the same factory).</para>
    /// 
    /// <para>If the cache contains a task that will throw an exception in the future, the same
    /// task instance is returned to all the callers of this method. This means that any given
    /// caller of this method should anticipate the type of exceptions that could be thrown from
    /// the Task used by any of the callers of this method.</para>
    /// 
    /// <para>To prevent the problem described above, as a convention, all the call sites of this method (if more
    /// than one) should use the same <paramref name="taskFactory"/> parameter and be prepared for the
    /// exceptions that it could throw.</para>
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="cache"></param>
    /// <param name="key">Key that matches the wanted return value.</param>
    /// <param name="taskFactory">Function that is run only if a Task for the given key is not already present in the cache.</param>
    /// <param name="expireOnCompletion">When true removes the Task from the cache as soon as it has completed (succeeded/failed/cancelled).</param>
    /// <returns>Returned <see cref="Task{TResult}"/> object may still be running or have already completed. Note that the Task might result in an exception.</returns>
    public static async Task<T> GetOrCreateTask<T>(this IMemoryCache cache, string key, Func<ICacheEntry, Task<T>> taskFactory, bool expireOnCompletion = false)
    {
        var asyncLazyValue = cache.GetOrCreate(
            key,
            entry => !expireOnCompletion
                ? new AsyncLazy<T>(() => taskFactory(entry))
                : new AsyncLazy<T>(
                    () => taskFactory(entry)
                        .ContinueWith(task => {
                            //Add an expiration token that triggers when the Task has completed (succeeded/failed/cancelled)
                            entry.AddExpirationToken(new TaskChangeToken(task));
                            return task;
                        })
                        .Unwrap()
                )
        )!;

        try {
            var result = await asyncLazyValue.ConfigureAwait(false);

            //The awaited Task has completed. Check that the task still is the same version
            //that the cache returns (i.e. the awaited task has not been invalidated during the await).
            if (cache.TryGetValue(key, out var existingValue)) {
                if (existingValue != asyncLazyValue) {
                    // The awaited value is no more the most recent one.
                    // Get the most recent value with a recursive call.
                    return await cache.GetOrCreateTask(key, taskFactory, expireOnCompletion)
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


    /// <summary>
    /// <para>Return the <see cref="Task"/> value for a given key from the cache. If the key is not already
    /// present in cache, <paramref name="taskFactory"/> will be run first to create the Task and it will be
    /// added to the cache immediately.</para>
    /// 
    /// <para>This method differs from <see cref="CacheExtensions.GetOrCreateAsync{TItem}"/>, which only caches
    /// the result of a Task, after it has successfully completed. Instead, this method caches a Lazy instance
    /// of the Task itself - allowing long-running/expensive asynchronous operations to be shared and avoiding
    /// concurrent duplication of work.</para>
    /// 
    /// <para>Often when a running Task is returned, it is a Task returned by the <paramref name="taskFactory"/>
    /// the caller has given as a parameter, but the returned task might also have a different origin (from
    /// another call elsewhere using the same key and presumably the same factory).</para>
    /// 
    /// <para>If the cache contains a task that will throw an exception in the future, the same
    /// task instance is returned to all the callers of this method. This means that any given
    /// caller of this method should anticipate the type of exceptions that could be thrown from
    /// the Task used by any of the callers of this method.</para>
    /// 
    /// <para>To prevent the problem described above, as a convention, all the call sites of this method (if more
    /// than one) should use the same <paramref name="taskFactory"/> parameter and be prepared for the
    /// exceptions that it could throw.</para>
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="key">Key that matches the wanted return value.</param>
    /// <param name="taskFactory">Function that is run only if a Task for the given key is not already present in the cache.</param>
    /// <param name="expireOnCompletion">When true removes the Task from the cache as soon as it has completed (succeeded/failed/cancelled).</param>
    /// <returns>Returned <see cref="Task"/> object may still be running or have already completed. Note that the Task might result in an exception.</returns>
    public static async Task GetOrCreateTask(this IMemoryCache cache, string key, Func<ICacheEntry, Task> taskFactory, bool expireOnCompletion = false)
    {
        var asyncLazyValue = cache.GetOrCreate(
            key,
            entry => !expireOnCompletion
                ? new AsyncLazy(() => taskFactory(entry))
                : new AsyncLazy(
                    () => taskFactory(entry)
                        .ContinueWith(task => {
                            //Add an expiration token that triggers when the Task has completed (succeeded/failed/cancelled)
                            entry.AddExpirationToken(new TaskChangeToken(task));
                            return task;
                        })
                        .Unwrap()
                )
        )!;

        try {
            await asyncLazyValue.ConfigureAwait(false);

            //The awaited Task has completed. Check that the task still is the same version
            //that the cache returns (i.e. the awaited task has not been invalidated during the await).
            if (cache.TryGetValue(key, out var existingValue)) {
                if (existingValue != asyncLazyValue) {
                    // The awaited value is no more the most recent one.
                    // Get the most recent value with a recursive call.
                    await cache.GetOrCreateTask(key, taskFactory, expireOnCompletion)
                        .ConfigureAwait(false);
                }
            }

        } catch (Exception) {
            //Task object for the given key failed with exception. Remove the task from the cache.
            cache.Remove(key);
            //Re-throw the exception to be handled by the caller.
            throw;
        }
    }

}