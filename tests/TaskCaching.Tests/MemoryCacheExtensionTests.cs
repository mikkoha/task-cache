using Microsoft.Extensions.Caching.Memory;

using Xunit;


namespace TaskCaching;

public class MemoryCacheExtensionTests : IDisposable
{

    [Fact]
    public async Task GetOrCreateTask_RunsFactoryOnlyOnce()
    {
        int factoryRunCount = 0;
        string testkey = "key1";

        Task TaskFactory(ICacheEntry entry) {
            Interlocked.Increment(ref factoryRunCount);
            return Task.CompletedTask;
        }

        var task1 = _memCache.GetOrCreateTask(testkey, TaskFactory);
        var task2 = _memCache.GetOrCreateTask(testkey, TaskFactory);
        var task3 = _memCache.GetOrCreateTask(testkey, TaskFactory);

        await Task.WhenAll(task1, task2, task3);

        //Factory should have only executed once
        Assert.Equal(1, factoryRunCount);
    }


    public void Dispose()
        => _memCache?.Dispose();


    private readonly IMemoryCache _memCache = new MemoryCache(new MemoryCacheOptions());

}
