﻿namespace TaskCaching;

/// <summary>
/// Represents a cache that stores and retrieves tasks by keys.
/// </summary>
public interface ITaskCache : IDisposable
{
    /// <summary>
    /// <para>Return from the cache the value for the given key. If value is already present in cache,
    /// that value will be returned. Otherwise value is first generated with the given method.</para>
    /// 
    /// <para>Return value can be a completed or running task-object. If the task-object is completed,
    /// it has run successfully to completion. Most often when a running task is returned,
    /// it is the task returned by the function the caller has given as a parameter, but the
    /// returned task might also have a different origin (from another call to this same method).
    /// If the cache contains a task that will end up throwing an exception in the future, the same
    /// task instance is returned to all the callers of this method. This means that any given
    /// caller of this method should anticipate the type of exceptions that could be thrown from
    /// the updateFunc used by any of the caller of this method.</para>
    /// 
    /// <para>To prevent the problem described above, as a convention, all the call sites of his method
    /// (if more than one) should use the same updateFunc-parameter and also be prepared for the
    /// exceptions that the the updateFunc could throw.</para>
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="key">The key that matches the value in the cache.</param>
    /// <param name="valueFactory">Function that is run only if a Task for the given key is not already present in the cache.</param>
    /// <param name="expireOnCompletion">When true removes the Task from the cache as soon as it has completed (succeeded/failed/cancelled).</param>
    /// <returns>Returned task-object can be completed or running. Note that the task might result in an exception.</returns>
    Task<T> AddOrGetExisting<T>(string key, Func<Task<T>> valueFactory, bool expireOnCompletion = false);

    /// <summary>
    /// Invalidates the value for the given key, if it exists in cache.
    /// </summary>
    /// <param name="key">The key of the value in cache to invalidate.</param>
    void Invalidate(string key);

    /// <summary>
    /// Determines whether a value exists in the cache for the given key.
    /// </summary>
    /// <param name="key">The key of the value to look for.</param>
    /// <returns>True if the cache contains a value with the given key, otherwise false.</returns>
    bool Contains(string key);

    /// <summary>
    /// Empties the cache of all entries.
    /// </summary>
    void Clear();
}
