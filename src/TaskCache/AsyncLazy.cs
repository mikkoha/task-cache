using System.Runtime.CompilerServices;


namespace TaskCaching;

internal class AsyncLazy<T> : Lazy<Task<T>>
{
    public AsyncLazy(Func<T> factory)
        : base(() => Task.Run(factory))
    { }


    public AsyncLazy(Func<Task<T>> factory)
        : base(() => Task.Run<T>(factory))
    { }


    public TaskAwaiter<T> GetAwaiter()
        => Value.GetAwaiter();


    public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
        => Value.ConfigureAwait(continueOnCapturedContext);
}