using Microsoft.Extensions.Caching.Memory;


namespace TaskCaching;

public class TaskCacheEntryOptions : MemoryCacheEntryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether a cache entry will be removed immediately upon the cached
    /// <see cref="System.Threading.Tasks.Task{T}"/> completing.
    /// </summary>
    public bool ExpirationOnCompletion { get; set; }
}