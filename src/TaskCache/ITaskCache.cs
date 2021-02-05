using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskCache
{
    public interface ITaskCache
    {
        /// <summary>
        /// Return from the cache the value for the given key. If value is already present in cache,
        /// that value will be returned. Otherwise value is first generated with the given method.
        ///
        /// Return value can be a completed or running task-object. If the task-object is completed,
        /// it has run succesfully to completion. Most often when a running task is returned,
        /// it is the task returned by the function the caller has given as a parameter, but the
        /// returned task might also have a different origin (from another call to this same method).
        /// If the cache contains a task that will end up throwing an exception in the future, the same
        /// task instance is returned to all the callers of this method. This means that any given
        /// caller of this method should anticipate the type of exceptions that could be thrown from
        /// the updateFunc used by any of the caller of this method.
        ///
        /// To prevent the problem described above, as a convention, all the call sites of his method
        /// (if more than one) should use the same updateFunc-parameter and also be prepared for the
        /// exceptions that the the updateFunc could throw.
        /// </summary>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <param name="key">Key that matches the wanted return value.</param>
        /// <param name="valueFactory">Function that is run only if a value for the given key is not already present in the cache.</param>
        /// <returns>Returned task-object can be completed or running. Note that the task might result in exception.</returns>
        Task<T> AddOrGetExisting<T>(string key, Func<Task<T>> valueFactory);

        /// <summary>
        /// Invalidate the value for the given key, if value exists.
        /// </summary>
        /// <param name="key"></param>
        void Invalidate(string key);

        /// <summary>
        /// Does the cache alrealy contain a value for the key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool Contains(string key);

        /// <summary>
        /// Empties the cache from all entries.
        /// </summary>
        void Clear();
    }
}
