using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

using Nito.AsyncEx;


namespace TaskCache
{
    public class TaskCache : ITaskCache
    {
        public async Task<T> AddOrGetExisting<T>(string key, Func<Task<T>> valueFactory) {

            var asyncLazyValue = new AsyncLazy<T>(valueFactory);

            var existingValue = _cache.GetOrCreate(key, entry => {
                entry.AddExpirationToken(new CancellationChangeToken(_cts.Token));
                return asyncLazyValue;
            });

            if (existingValue != null) {
                asyncLazyValue = existingValue;
            }

            try {
                var result = await asyncLazyValue;

                // The awaited Task has completed. Check that the task still is the same version
                // that the cache returns (i.e. the awaited task has not been invalidated during the await).
                if (asyncLazyValue != _cache.GetOrCreate(key, _ => new AsyncLazy<T>(valueFactory))) {
                    // The awaited value is no more the most recent one.
                    // Get the most recent value with a recursive call.
                    return await AddOrGetExisting(key, valueFactory);
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
