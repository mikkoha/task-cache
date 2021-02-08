using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Xunit;
using Xunit.Sdk;


namespace TaskCaching
{
    public class TaskCacheTests
    {
        /// <summary>
        /// Test that the cache returns the value that the valueFactory function generates.
        /// </summary>
        [Fact]
        public async Task AddOrGetExisting_ReturnsValueFromValueFactory()
        {
            string testValue = "value1";
            Func<Task<TestValue>> valueFactory = () => Task.FromResult(new TestValue(testValue));

            var value = await _cache.AddOrGetExisting("key1", valueFactory);

            Assert.Equal(testValue, value.Value);
        }


        /// <summary>
        /// Test that for subsequent calls the value is only generated once, and after that
        /// the same generated value is returned.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task AddOrGetExisting_GeneratesTheValueOnlyOnce() {
            string testValue = "value1";
            string testkey = "key1";
            int valueGeneratedTimes = 0;
            Func<Task<TestValue>> valueFactory = () => {
                valueGeneratedTimes++;
                return Task.FromResult(new TestValue(testValue));
            };

            var value1 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.Equal(testValue, value1.Value);

            var value2 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.Equal(testValue, value2.Value);

            var value3 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.Equal(testValue, value3.Value);

            //Value should be generated only once.
            Assert.Equal(1, valueGeneratedTimes);
        }


        [Fact]
        public async Task AddOrGetExisting_ValueIsRebuildAfterInvalidation() {
            string testValue = "value1";
            string testkey = "key1";
            int valueGeneratedTimes = 0;
            Func<Task<TestValue>> valueFactory = () => {
                valueGeneratedTimes++;
                return Task.FromResult(new TestValue(testValue));
            };

            var value1 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.Equal(testValue, value1.Value);

            var value2 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.Equal(testValue, value2.Value);

            //Value should be generated only once.
            Assert.Equal(1, valueGeneratedTimes);

            _cache.Invalidate(testkey);

            var value3 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.Equal(testValue, value3.Value);

            //Value should be regenerated after invalidation.
            Assert.Equal(2, valueGeneratedTimes);
        }


        /// <summary>
        /// Modifying a key-value-pair in the cache should not affect other key-value-pairs
        /// (when eviction policies are not causing changes).
        /// </summary>
        [Fact]
        public async Task AddOrGetExisting_DifferentKeysInCacheFunctionIndependently() {
            string testValue1 = "value1";
            string testValue2 = "value2";
            string testkey1 = "key1";
            string testkey2 = "key2";

            int value1GeneratedTimes = 0;
            Func<Task<TestValue>> buildValue1Func = () => {
                value1GeneratedTimes++;
                return Task.FromResult(new TestValue(testValue1));
            };

            int value2GeneratedTimes = 0;
            Func<Task<TestValue>> buildValue2Func = () => {
                value2GeneratedTimes++;
                return Task.FromResult(new TestValue(testValue2));
            };


            var value1get1 = await _cache.AddOrGetExisting(testkey1, buildValue1Func);
            var value2get1 = await _cache.AddOrGetExisting(testkey2, buildValue2Func);
            var value1get2 = await _cache.AddOrGetExisting(testkey1, buildValue1Func);
            var value2get2 = await _cache.AddOrGetExisting(testkey2, buildValue2Func);

            //Value 1 should be built only once.
            Assert.Equal(1, value1GeneratedTimes);
            //Value 2 should be built only once.
            Assert.Equal(1, value1GeneratedTimes);

            Assert.Equal(testValue1, value1get1.Value);
            Assert.Equal(testValue1, value1get2.Value);
            Assert.Equal(testValue2, value2get1.Value);
            Assert.Equal(testValue2, value2get2.Value);

            // Invalidation should affect only the right key-value-pair.
            _cache.Invalidate(testkey1);

            var value1get3 = await _cache.AddOrGetExisting(testkey1, buildValue1Func);
            var value2get3 = await _cache.AddOrGetExisting(testkey2, buildValue2Func);

            //Value 1 should be rebuilt.
            Assert.Equal(2, value1GeneratedTimes);
            //Value 2 should (still) be built only once.
            Assert.Equal(1, value2GeneratedTimes);

            Assert.Equal(testValue1, value1get3.Value);
            Assert.Equal(testValue2, value2get3.Value);
        }


        [Fact]
        public async Task AddOrGetExisting_FailedTasksAreNotPersisted() {
            string testValue = "value1";
            string testkey = "key1";
            string exceptionMessage = "First two calls will fail.";

            int valueGeneratedTimes = 0;
            Func<Task<TestValue>> valueFactory = () => {
                valueGeneratedTimes++;

                return Task.Factory.StartNew(() => {
                    if (valueGeneratedTimes <= 2) {
                        throw new Exception(exceptionMessage);
                    }
                    return new TestValue(testValue);
                });
            };

            var cacheTask = _cache.AddOrGetExisting(testkey, valueFactory);
            await SilentlyHandleFaultingTask(cacheTask, exceptionMessage);
            //First value generation should fail.
            Assert.True(cacheTask.IsFaulted);
            //Value should be build 1 times.
            Assert.Equal(1, valueGeneratedTimes);

            cacheTask = _cache.AddOrGetExisting(testkey, valueFactory);
            await SilentlyHandleFaultingTask(cacheTask, exceptionMessage);
            //Second value generation should fail.
            Assert.True(cacheTask.IsFaulted);
            //Value should be build 2 times, because first failed.
            Assert.Equal(2, valueGeneratedTimes);

            cacheTask = _cache.AddOrGetExisting(testkey, valueFactory);
            var cacheValue = await cacheTask;
            //Value generation should succeed the third time.
            Assert.True(cacheTask.IsCompleted);
            //Value should be build 3 times, because first two times failed.
            Assert.Equal(3, valueGeneratedTimes);
            //Cache should return correct value.
            Assert.Equal(testValue, cacheValue.Value);

            cacheTask = _cache.AddOrGetExisting(testkey, valueFactory);
            cacheValue = await cacheTask;
            //Value generation should succeed the fourth time.
            Assert.True(cacheTask.IsCompleted);
            //Value should be build 3 times, because first two times failed, but third succeeded.
            Assert.Equal(3, valueGeneratedTimes);
            //Cache should return correct value.
            Assert.Equal(testValue, cacheValue.Value);
        }


        [Fact]
        public async Task Contains_ReturnsTrueWhenKeyExists() {
            string testkey1 = "key1";
            string testkey2 = "key2";

            Func<Task<TestValue>> valueFactory = () => {
                return Task.FromResult(new TestValue("test"));
            };


            Assert.False(_cache.Contains(testkey1));
            Assert.False(_cache.Contains(testkey2));

            await _cache.AddOrGetExisting(testkey1, valueFactory);

            Assert.True(_cache.Contains(testkey1));
            Assert.False(_cache.Contains(testkey2));
        }


        [Fact]
        public async Task AddOrGetExisting_ExceptionsFromValueGenerationCanBeHandled() {
            string testkey = "key";
            string testValue = "value";
            string testExceptionMessage = "this is exception";

            int valueFactoryCalledTimes = 0;
            Func<Task<TestValue>> valueFactory = () => {
                valueFactoryCalledTimes++;
                return Task.Factory.StartNew(() => {
                    Thread.Sleep(10);
                    if (valueFactoryCalledTimes != -9999) {
                        // Throw always
                        throw new Exception(testExceptionMessage);
                    }
                    return new TestValue(testValue);
                });
            };

            Exception exception = null;

            // Use the cache.
            try {
                var v = await _cache.AddOrGetExisting(testkey, async () => {
                    var res = await valueFactory();
                    return res.Value;
                });
                throw new XunitException("This point should never be reached, because the valueFactory should always throw.");
            } catch (Exception ex) {
                exception = ex;
            }

            //Exception from valueFactory should be caught.
            Assert.Equal(testExceptionMessage, exception.Message);

            // Use the cache again.
            try {
                var v = await _cache.AddOrGetExisting(testkey, async () => {
                    var res = await valueFactory();
                    return res.Value;
                });
                throw new XunitException("This point should never be reached, because the valueFactory should always throw.");
            } catch (Exception ex) {
                exception = ex;
            }

            //Value generation should be called two times, because failed results should be evicted from the cache.
            Assert.Equal(2, valueFactoryCalledTimes);
        }


        /// <summary>
        /// Test the following scenario:
        /// - Cache user A gets a Task from the cache and starts to await it.
        /// - While A is awaiting, the value is invalidated.
        /// - After the A's await is done, A should have a result that was generated after the invalidation,
        /// and not the original (now invalidated) result that A started to await in the first step. This
        /// "result switch" should be invisible to A.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task AddOrGetExisting_DoesNotReturnResultsThatWereInvalidatedDuringAwait() {
            string key = "key";
            string earlierValue = "first";
            string laterValue = "second";

            var laterTaskStart = new ManualResetEvent(false);
            var firstValueFactoryStarted = new ManualResetEvent(false);
            var laterValueFactoryStarted = new ManualResetEvent(false);
            var firstValueFactoryContinue = new ManualResetEvent(false);
            var laterValueFactoryContinue = new ManualResetEvent(false);

            int valueFactory1Executed = 0;
            int valueFactory2Executed = 0;

            Func<Task<TestValue>> valueFactory1 = () => {
                return Task.Factory.StartNew(() => {
                    firstValueFactoryStarted.Set();
                    firstValueFactoryContinue.WaitOne();
                    valueFactory1Executed++;
                    return new TestValue(earlierValue);
                });
            };

            Func<Task<TestValue>> valueFactory2 = () => {
                return Task.Factory.StartNew(() => {
                    laterValueFactoryStarted.Set();
                    laterValueFactoryContinue.WaitOne();
                    valueFactory2Executed++;
                    return new TestValue(laterValue);
                });
            };


            var cacheUserTask1 = Task.Factory.StartNew(async () => {
                return await _cache.AddOrGetExisting(key, valueFactory1);
            });

            var cacheUserTask2 = Task.Factory.StartNew(async () => {
                laterTaskStart.WaitOne();
                return await _cache.AddOrGetExisting(key, valueFactory2);
            });

            // Wait until the first value get from cache is in the middle of the value generation.
            // At this point, a Task that is running but not completed has been added to the cache.
            // CacheUserTask1 is awaiting for the Task to complete.
            firstValueFactoryStarted.WaitOne();

            // While the first value get is still running, invalidate the value.
            _cache.Invalidate(key);

            // Second get from the cache can now begin.
            // Because the first (still uncompleted) value was invalidated, cacheUserTask2's fetch should start a new value generation.
            laterTaskStart.Set();

            // New value generation has started but not yet completed.
            laterValueFactoryStarted.WaitOne();

            // Let first value generation run to completion.
            firstValueFactoryContinue.Set();

            // Let second value generation run to completion.
            laterValueFactoryContinue.Set();

            await Task.WhenAll(new List<Task>() { cacheUserTask1, cacheUserTask2 });


            //The first fetch from the cache should have returned the value generated by the second fetch, because the first value was invalidated while still running.
            Assert.Equal(laterValue, cacheUserTask1.Result.Result.Value);
            //The second fetch should have returned the later value.
            Assert.Equal(laterValue, cacheUserTask2.Result.Result.Value);

            //The first valueFactory should have been called once.
            Assert.Equal(1, valueFactory1Executed);
            //The second valueFactory should have been called once.
            Assert.Equal(1, valueFactory2Executed);
        }


        [Fact]
        public async Task Clear_InvalidatesAllResults()
        {
            string testkey1 = "key1";
            string testkey2 = "key2";
            string testkey3 = "key3";

            Task<TestValue> ValueFactory() => Task.FromResult(new TestValue("test"));

            Assert.False(_cache.Contains(testkey1));
            Assert.False(_cache.Contains(testkey2));
            Assert.False(_cache.Contains(testkey3));

            await _cache.AddOrGetExisting(testkey1, ValueFactory);
            await _cache.AddOrGetExisting(testkey2, ValueFactory);
            await _cache.AddOrGetExisting(testkey3, ValueFactory);

            Assert.True(_cache.Contains(testkey1));
            Assert.True(_cache.Contains(testkey2));
            Assert.True(_cache.Contains(testkey3));

            _cache.Clear();

            Assert.False(_cache.Contains(testkey1));
            Assert.False(_cache.Contains(testkey2));
            Assert.False(_cache.Contains(testkey3));
        }


        [Fact]
        public async Task AddOrGetExisting_InvalidatesCompletedResultsWhenExpirationOnCompletionSet() {
            string key = "key";
            string value = "test";

            //Configure so that tasks should be immediately removed from the cache the instant they complete.
            var expirationPolicy = new TaskCacheItemPolicy { ExpirationOnCompletion = true };

            var valueFactoryStarted = new ManualResetEvent(false);
            var valueFactoryContinue = new ManualResetEvent(false);

            Func<Task<TestValue>> valueFactory = () => {
                return Task.Factory.StartNew(() => {
                    valueFactoryStarted.Set();
                    valueFactoryContinue.WaitOne();
                    return new TestValue(value);
                });
            };

            //First assert that the value is not yet in the cache
            Assert.False(_cache.Contains(key));

            var cacheUserTask = _cache.AddOrGetExisting(key, valueFactory, expirationPolicy);

            // Wait until the value get from cache is in the middle of the value generation.
            // At this point, a Task that is running but not completed has been added to the cache.
            valueFactoryStarted.WaitOne();

            // While the value generation is still running, confirm that it is present in the cache.
            Assert.True(_cache.Contains(key));

            // Let value generation run to completion.
            valueFactoryContinue.Set();

            await cacheUserTask;

            // Assert that the value has now been invalidated and removed from the cache
            Assert.False(_cache.Contains(key));
        }


        private async Task SilentlyHandleFaultingTask(Task task, string expectedExceptionMessage) {
            try {
                await task;
            } catch (Exception ex) {
                Assert.Equal(expectedExceptionMessage, ex.Message);
            }
        }


        private readonly ITaskCache _cache = new TaskCache();


        private class TestValue
        {
            public string Value { get; set; }
            public TestValue(string value) {
                Value = value;
            }
        }
    }
}
