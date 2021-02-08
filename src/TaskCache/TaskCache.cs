using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

using Nito.AsyncEx;


namespace TaskCaching
{
    public class TaskCache : ITaskCache
    {
        public async Task<T> AddOrGetExisting<T>(string key, Func<Task<T>> valueFactory, TaskCacheItemPolicy policy = default) {
            var asyncLazyValue = _cache.GetOrCreate(key, entry => {
                //Add a common expiration token which is used for clearing the cache
                entry.AddExpirationToken(new CancellationChangeToken(_cts.Token));
                //Customize expiration as per the policy
                entry.AbsoluteExpiration = policy.AbsoluteExpiration;
                entry.AbsoluteExpirationRelativeToNow = policy.AbsoluteExpirationRelativeToNow;
                entry.SlidingExpiration = policy.SlidingExpiration;
                return new AsyncLazy<T>(
                    factory: () => !policy.ExpirationOnCompletion
                        ? valueFactory()
                        : valueFactory()
                            .ContinueWith(task => {
                                //Add an expiration token which triggers when the Task has completed (succeeded/failed/cancelled)
                                entry.AddExpirationToken(new TaskChangeToken(task)); 
                                return task;
                            })
                            .Unwrap()
                );
            });

            try {
                var result = await asyncLazyValue.ConfigureAwait(false);

                // The awaited Task has completed. Check that the task still is the same version
                // that the cache returns (i.e. the awaited task has not been invalidated during the await).
                if (_cache.TryGetValue(key, out var existingValue)) {
                    if (existingValue != asyncLazyValue) {
                        // The awaited value is no more the most recent one.
                        // Get the most recent value with a recursive call.
                        return await AddOrGetExisting(key, valueFactory, policy).ConfigureAwait(false);
                    }
                }

                return result;

            } catch (Exception) {
                // Task object for the given key failed with exception. Remove the task from the cache.
                _cache.Remove(key);
                // Re throw the exception to be handled by the caller.
                throw;
            }
        }


        public void Invalidate(string key)
            => _cache.Remove(key);


        public bool Contains(string key)
            => _cache.TryGetValue(key, out _);


        public void Clear() {
            var cts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            cts.Cancel();
            cts.Dispose();
        }


        private CancellationTokenSource _cts = new CancellationTokenSource();


        private readonly MemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    }
}
