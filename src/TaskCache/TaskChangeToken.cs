using Microsoft.Extensions.Primitives;


namespace TaskCaching;

public class TaskChangeToken : IChangeToken
{
    public TaskChangeToken(Task task)
        => _task = task;


    public bool HasChanged => _task.IsCompleted;


    public bool ActiveChangeCallbacks { get; private set; } = true;


    public IDisposable RegisterChangeCallback(Action<object> callback, object state) {
        try {
            return _task.ContinueWith((t, o) => callback(o), state);
        } catch (ObjectDisposedException) {
            ActiveChangeCallbacks = false;
        }
        return NullDisposable.Instance;
    }


    private readonly Task _task;


    private class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new NullDisposable();
        public void Dispose() { }
    }
}