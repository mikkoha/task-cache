using System;


namespace TaskCache
{
    public struct TaskCacheItemPolicy
    {
        /// <summary>
        /// Gets or sets an absolute expiration date for the cache entry.
        /// </summary>
        public DateTimeOffset? AbsoluteExpiration { get; set; }

        /// <summary>
        /// Gets or sets an absolute expiration time, relative to now.
        /// </summary>
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        /// <summary>
        /// Gets or sets how long a cache entry can be inactive (e.g. not accessed) before it will be removed.
        /// This will not extend the entry lifetime beyond the absolute expiration (if set).
        /// </summary>
        public TimeSpan? SlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a cache entry will be removed immediately upon the cached
        /// <see cref="System.Threading.Tasks.Task{T}"/> completing.
        /// </summary>
        public bool ExpirationOnCompletion { get; set; }
    }
}
