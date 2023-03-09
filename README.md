# TaskCache
[See my blog post for full explanation.](https://tech.mikkohaapanen.com/net-c-cache-class-for-caching-task-objects/)

TaskCache is a C# .NET custom cache implementation targeted for use cases where long lasting Task-operations need to be cached.

This implementation uses `Microsoft.Extensions.Caching.Memory.MemoryCache` and persists `Lazy<Task<T>>` objects in it.

The implementation ensures that
- No parallel or unnecessary operations to get a value will be started.
- Failed Tasks are not cached. (No negative caching.)
- Cache users can't get invalidated results from the cache, even if the value is invalidated *during* an await.
- Optionally, Tasks can be automatically evicted from the cache as soon as they complete.
