using Microsoft.Extensions.Primitives;


namespace TaskCaching.Internals;

internal class TaskChangeToken : IChangeToken
{
    public TaskChangeToken(Task task)
        => _task = task;


    public bool HasChanged => _task.IsCompleted;


    public bool ActiveChangeCallbacks { get; private set; } = true;


    public IDisposable RegisterChangeCallback(Action<object> callback, object state)
    {
        try {
            return _task.ContinueWith((t, o) => callback(o), state);
        } catch (ObjectDisposedException) {
            ActiveChangeCallbacks = false;
        }
        return EmptyDisposable.Instance;
    }


    private readonly Task _task;


    private class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();
        public void Dispose() { }
    }
}