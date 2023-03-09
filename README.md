# TaskCaching

[See my blog post for full explanation.](https://tech.mikkohaapanen.com/net-c-cache-class-for-caching-task-objects/)

TaskCaching provides a means for easily caching long-lasting or expensive `Task` operations in .NET.

Its features ensure that:
 * No parallel or unnecessary operations to get a value will be started.
 * Failed Tasks are not cached (no negative caching).
 * Cache users can't get invalidated results from the cache, even if the value is invalidated *during* an await.
 * Optionally, Tasks can be automatically evicted from the cache as soon as they complete.
 * It targets the .NET Standard 2.0 so should be usable in most .NET projects.


Currently there are two packages/projects:

## TaskCaching.Microsoft.Extensions.Caching.Memory

**This should probably be the go-to implementation right now.**

The *TaskCaching.Microsoft.Extensions.Caching.Memory* project has a dependency on ANY version of
Microsoft's [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory) NuGet package.

It adds an `IMemoryCache.GetOrCreateTask<T>(...)` extension method that persists `Lazy<Task<T>>` objects in the cache, fulfilling the above mentioned features.

This method differs from Microsoft's `IMemoryCache.GetOrCreateAsync<T>(...)`, which only caches the result of a Task after it has successfully completed.
Instead, this method caches a Lazy instance of the Task itself without waiting for it - allowing long-running/expensive asynchronous operations to be shared
and avoiding concurrent duplication of work.

### How to use it?

Install the NuGet package first, E.g.:

```csharp
dotnet add package TaskCaching.Microsoft.Extensions.Caching.Memory
```

Now simply take an existing MemoryCache object or create a new one and use the GetOrCreateTask extension-method:

```csharp
async Task<int> DoSomeSlowTask(int i) {
    Console.WriteLine("Waiting " + i);
    await Task.Delay(1000);
    Console.WriteLine("Waited " + i);
    return i;
}

using var cache = new MemoryCache(new MemoryCacheOptions());

//The following calls to the cache are going to run concurrently as we're not awaiting them yet
var task1 = cache.GetOrCreateTask("uniqueKeyForTask", e => DoSomeSlowTask(1));
var task2 = cache.GetOrCreateTask("uniqueKeyForTask", e => DoSomeSlowTask(2)); //This call to DoSomeSlowTask(2) will not run
var task3 = cache.GetOrCreateTask("uniqueKeyForTask", e => DoSomeSlowTask(3)); //This call to DoSomeSlowTask(3) will not run

await Task.WhenAll(task1, task2, task3);

Assert.Equal(1, await task1); //The result of DoSomeSlowTask(1) was returned by the cache
Assert.Equal(1, await task2); //The result of DoSomeSlowTask(1) was returned by the cache
Assert.Equal(1, await task3); //The result of DoSomeSlowTask(1) was returned by the cache
```

What if you don't want completed tasks remaining in the cache? You could manually remove them, or set appropriate expiration policies, but alternatively you can tell `GetOrCreateTask` to evict them as soon as they complete:

```csharp
async Task<int> DoSomeSlowTask(int i) {
    Console.WriteLine("Waiting " + i);
    await Task.Delay(1000);
    Console.WriteLine("Waited " + i);
    return i;
}

using var cache = new MemoryCache(new MemoryCacheOptions());

//These following cache calls are being awaited so will run in sequence, however the DoSomeSlowTask(1) task will 
//immediately be evicted upon its completion, before the next cache call, because of the expireOnCompletion parameter
var value1 = await cache.GetOrCreateTask("uniqueKeyForTask", e => DoSomeSlowTask(1), expireOnCompletion:true);

//The DoSomeSlowTask(1) task is no longer in the cache anymore, so DoSomeSlowTask(2) will now run
var value2 = await cache.GetOrCreateTask("uniqueKeyForTask", e => DoSomeSlowTask(2), expireOnCompletion:true);

Assert.NotEqual(value1, value2);
```


## TaskCaching

The `ITaskCache` and `TaskCache` in this are legacy of the original project.

Refer to the earlier blog post (https://tech.mikkohaapanen.com/net-c-cache-class-for-caching-task-objects/) to read more about this.

The blog post may be quite old now, but is mostly still relevant. The primary things that have changed are:
 * TaskCache USED to use System.Runtime.Caching.MemoryCache internally, but now uses [Microsoft.Extensions.Caching.Memory](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory) instead.
 * TaskCache is now just a thin wrapper around a MemoryCache instance and calls the `GetOrCreateTask` extension method.
