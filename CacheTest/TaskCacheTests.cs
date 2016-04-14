using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TaskCacheExample.Tests
{
    [TestFixture]
    public class TaskCacheTests
    {
        private class TestValue
        {
            public string Value { get; set; }

            public TestValue(string value)
            {
                Value = value;
            }
        }


        private ITaskCache _cache;

        [SetUp]
        public void Setup()
        {
            _cache = new TaskCache();
            _cache.Clear();
        }

        /// <summary>
        /// Test that the cache returns the value that the valueFactory function generates.
        /// </summary>
        [Test]
        public async Task AddOrGetExisting_ReturnsValueFromValueFactory()
        {
            string testValue = "value1";
            Func<Task<TestValue>> valueFactory = () => Task.FromResult(new TestValue(testValue));

            var value = await _cache.AddOrGetExisting("key1", valueFactory);

            Assert.AreEqual(testValue, value.Value);
        }

        /// <summary>
        /// Test that for subsequent calls the value is only generated once, and after that
        /// the same generated value is returned.
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task AddOrGetExisting_GeneratesTheValueOnlyOnce()
        {
            string testValue = "value1";
            string testkey = "key1";
            int valueGeneratedTimes = 0;
            Func<Task<TestValue>> valueFactory = () =>
            {
                valueGeneratedTimes++;
                return Task.FromResult(new TestValue(testValue));
            };

            var value1 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.AreEqual(testValue, value1.Value);

            var value2 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.AreEqual(testValue, value2.Value);

            var value3 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.AreEqual(testValue, value3.Value);

            Assert.AreEqual(1, valueGeneratedTimes, "Value should be generated only once.");
        }

        [Test]
        public async Task AddOrGetExisting_ValueIsRebuildAfterInvalidation()
        {
            string testValue = "value1";
            string testkey = "key1";
            int valueGeneratedTimes = 0;
            Func<Task<TestValue>> valueFactory = () =>
            {
                valueGeneratedTimes++;
                return Task.FromResult(new TestValue(testValue));
            };

            var value1 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.AreEqual(testValue, value1.Value);

            var value2 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.AreEqual(testValue, value2.Value);

            Assert.AreEqual(1, valueGeneratedTimes, "Value should be generated only once.");

            _cache.Invalidate(testkey);

            var value3 = await _cache.AddOrGetExisting(testkey, valueFactory);
            Assert.AreEqual(testValue, value3.Value);

            Assert.AreEqual(2, valueGeneratedTimes, "Value should be regenerated after invalidation.");
        }

        /// <summary>
        /// Modifying a key-value-pair in the cache should not affect other key-value-pairs
        /// (when eviction policys are not causing changes).
        /// </summary>
        [Test]
        public async Task AddOrGetExisting_DifferentKeysInCacheFunctionIndependently()
        {
            string testValue1 = "value1";
            string testValue2 = "value2";
            string testkey1 = "key1";
            string testkey2 = "key2";

            int value1GeneratedTimes = 0;
            Func<Task<TestValue>> buildValue1Func = () =>
            {
                value1GeneratedTimes++;
                return Task.FromResult(new TestValue(testValue1));
            };

            int value2GeneratedTimes = 0;
            Func<Task<TestValue>> buildValue2Func = () =>
            {
                value2GeneratedTimes++;
                return Task.FromResult(new TestValue(testValue2));
            };


            var value1get1 = await _cache.AddOrGetExisting(testkey1, buildValue1Func);
            var value2get1 = await _cache.AddOrGetExisting(testkey2, buildValue2Func);
            var value1get2 = await _cache.AddOrGetExisting(testkey1, buildValue1Func);
            var value2get2 = await _cache.AddOrGetExisting(testkey2, buildValue2Func);

            Assert.AreEqual(1, value1GeneratedTimes, "Value 1 should be built only once.");
            Assert.AreEqual(1, value1GeneratedTimes, "Value 2 should be built only once.");

            Assert.AreEqual(testValue1, value1get1.Value);
            Assert.AreEqual(testValue1, value1get2.Value);
            Assert.AreEqual(testValue2, value2get1.Value);
            Assert.AreEqual(testValue2, value2get2.Value);

            // Invalidation should affect only the right key-value-pair.
            _cache.Invalidate(testkey1);

            var value1get3 = await _cache.AddOrGetExisting(testkey1, buildValue1Func);
            var value2get3 = await _cache.AddOrGetExisting(testkey2, buildValue2Func);

            Assert.AreEqual(2, value1GeneratedTimes, "Value 1 should be rebuilt.");
            Assert.AreEqual(1, value2GeneratedTimes, "Value 2 should (still) be built only once.");

            Assert.AreEqual(testValue1, value1get3.Value);
            Assert.AreEqual(testValue2, value2get3.Value);
        }

        [Test]
        public async Task AddOrGetExisting_FailedTasksAreNotPersisted()
        {
            string testValue = "value1";
            string testkey = "key1";
            string exceptionMessage = "First two calls will fail.";

            int valueGeneratedTimes = 0;
            Func<Task<TestValue>> valueFactory = () =>
            {
                valueGeneratedTimes++;

                return Task.Factory.StartNew(() =>
                {
                    if (valueGeneratedTimes <= 2)
                    {
                        throw new Exception(exceptionMessage);
                    }
                    return new TestValue(testValue);
                });
            };

            var cacheTask = _cache.AddOrGetExisting(testkey, valueFactory);
            await SilentlyHandleFaultingTask(cacheTask, exceptionMessage);
            Assert.IsTrue(cacheTask.IsFaulted, "First value generation should fail.");
            Assert.AreEqual(1, valueGeneratedTimes, "Value should be build 1 times.");

            cacheTask = _cache.AddOrGetExisting(testkey, valueFactory);
            await SilentlyHandleFaultingTask(cacheTask, exceptionMessage);
            Assert.IsTrue(cacheTask.IsFaulted, "Second value generation should fail.");
            Assert.AreEqual(2, valueGeneratedTimes, "Value should be build 2 times, because first failed.");

            cacheTask = _cache.AddOrGetExisting(testkey, valueFactory);
            var cacheValue = await cacheTask;
            Assert.IsTrue(cacheTask.IsCompleted, "Value generation should succeed the third time.");
            Assert.AreEqual(3, valueGeneratedTimes, "Value should be build 3 times, because first two times failed.");
            Assert.AreEqual(testValue, cacheValue.Value, "Cache should return correct value.");

            cacheTask = _cache.AddOrGetExisting(testkey, valueFactory);
            cacheValue = await cacheTask;
            Assert.IsTrue(cacheTask.IsCompleted, "Value generation should succeed the fourth time.");
            Assert.AreEqual(3, valueGeneratedTimes, "Value should be build 3 times, because first two times failed, but third succeeded.");
            Assert.AreEqual(testValue, cacheValue.Value, "Cache should return correct value.");
        }

        [Test]
        public async Task Contains_ReturnsTrueWhenKeyExists()
        {
            string testkey1 = "key1";
            string testkey2 = "key2";

            Func<Task<TestValue>> valueFactory = () =>
            {
                return Task.FromResult(new TestValue("test"));
            };


            Assert.AreEqual(false, _cache.Contains(testkey1));
            Assert.AreEqual(false, _cache.Contains(testkey2));

            await _cache.AddOrGetExisting(testkey1, valueFactory);

            Assert.AreEqual(true, _cache.Contains(testkey1));
            Assert.AreEqual(false, _cache.Contains(testkey2));
        }

        [Test]
        public async Task AddOrGetExisting_ExceptionsFromValueGenerationCanBeHandled()
        {
            string testkey = "key";
            string testValue = "value";
            string testExceptionMessage = "this is exception";

            int valueFactoryCalledTimes = 0;
            Func<Task<TestValue>> valueFactory = () =>
            {
                valueFactoryCalledTimes++;
                return Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(10);
                    if (valueFactoryCalledTimes != -9999)
                    {
                        // Throw always
                        throw new Exception(testExceptionMessage);
                    }
                    return new TestValue(testValue);
                });
            };

            Exception exception = null;

            // Use the cache.
            try
            {
                var v = await _cache.AddOrGetExisting(testkey, async () =>
                {
                    var res = await valueFactory();
                    return res.Value;
                });
                Assert.Fail("This point should never be reached, because the valueFactory should always throw.");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.AreEqual(testExceptionMessage, exception.Message, "Exception from valueFactory should be catched.");

            // Use the cache again.
            try
            {
                var v = await _cache.AddOrGetExisting(testkey, async () =>
                {
                    var res = await valueFactory();
                    return res.Value;
                });
                Assert.Fail("This point should never be reached, because the valueFactory should always throw.");
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.AreEqual(2, valueFactoryCalledTimes, "Value generation should be called two times, because failed results should be evicted from the cache.");
        }

        /// <summary>
        /// Voi olla tilanne, jossa
        /// - A hakee taskin cachesta ja alkaa odottaa (await) sitä.
        /// - Arvo invalidoidaan sillä aikaa kun odotus on kesken.
        /// - Tällöin A:lle pitäisi tarjota odotuksen (await) pääteeksi tuoreempi arvo, eikä valmiiksi vanhentunutta.
        ///
        /// Test following scenario:
        /// - Cache user A gets a Task from the cache and starts to await it.
        /// - While A is awaiting, the value is invalidated.
        /// - After the A's await is done, A should have a result that was generated after the invalidation, and not the original (now invalidated) result that A started to await in the first step. This "result swich" should be invicible to A.
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task AddOrGetExisting_DoesNotReturnResultsThatWereInvalidatedDuringAwait()
        {
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

            Func<Task<TestValue>> valueFactory1 = () =>
            {
                return Task.Factory.StartNew(() =>
                {
                    firstValueFactoryStarted.Set();
                    firstValueFactoryContinue.WaitOne();
                    valueFactory1Executed++;
                    return new TestValue(earlierValue);
                });
            };

            Func<Task<TestValue>> valueFactory2 = () =>
            {
                return Task.Factory.StartNew(() =>
                {
                    laterValueFactoryStarted.Set();
                    laterValueFactoryContinue.WaitOne();
                    valueFactory2Executed++;
                    return new TestValue(laterValue);
                });
            };


            var cacheUserTask1 = Task.Factory.StartNew(async () =>
            {
                return await _cache.AddOrGetExisting(key, valueFactory1);
            });

            var cacheUserTask2 = Task.Factory.StartNew(async () =>
            {
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


            Assert.AreEqual(laterValue, cacheUserTask1.Result.Result.Value,
                "The first fetch from the cache should have returned the value generated by the second fetch, because the first value was invalidated while still running.");
            Assert.AreEqual(laterValue, cacheUserTask2.Result.Result.Value,
                "The second fetch should have returned the later value.");

            Assert.AreEqual(1, valueFactory1Executed, "The first valueFactory should have been called once.");
            Assert.AreEqual(1, valueFactory2Executed, "The second valueFactory should have been called once.");
        }

        private async Task SilentlyHandleFaultingTask(Task task, string expectedExceptionMessage)
        {
            try
            {
                await task;
            }
            catch(Exception ex)
            {
                Assert.AreEqual(expectedExceptionMessage, ex.Message);
            }
        }
    }
}
