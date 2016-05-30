using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Xunit;

namespace NHibernate.Caches.Redis.Tests
{
    public class RedisCacheTests : RedisTest
    {
        private readonly RedisCacheProviderOptions options;

        public RedisCacheTests()
        {
            options = CreateTestProviderOptions();
        }

        [Fact]
        void Configure_cache_expiration()
        {
            var configuration = new RedisCacheConfiguration("region") { Expiration = TimeSpan.FromMinutes(99) };
            var sut = new RedisCache(configuration, ConnectionMultiplexer, options);

            sut.Put(999, new Person("Foo", 10));

            var cacheKey = sut.CacheNamespace.GetKey(999);
            var expiry = Redis.KeyTimeToLive(cacheKey);
            Assert.InRange(expiry.Value, low: TimeSpan.FromMinutes(98), high: TimeSpan.FromMinutes(99));
        }

        [Fact]
        void Configure_cache_lock_timeout()
        {
            var configuration = new RedisCacheConfiguration("region") { LockTimeout = TimeSpan.FromSeconds(123) };
            var sut = new RedisCache(configuration, ConnectionMultiplexer, options);
            const string key = "123";
            
            sut.Lock(key);
            var lockKey = sut.CacheNamespace.GetLockKey(key);

            var expiry = Redis.KeyTimeToLive(lockKey);
            Assert.InRange(expiry.Value, low: TimeSpan.FromSeconds(120), high: TimeSpan.FromSeconds(123));
        }

        [Fact]
        void Put_adds_the_item_to_the_cache()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            sut.Put(999, new Person("Foo", 10));

            var cacheKey = sut.CacheNamespace.GetKey(999);
            var data = Redis.StringGet(cacheKey);
            var person = (Person)options.Serializer.Deserialize(data);
            Assert.Equal("Foo", person.Name);
            Assert.Equal(10, person.Age);
        }

        [Fact]
        void Put_sets_an_expiration_on_the_item()
        {
            var config = new RedisCacheConfiguration("region") { Expiration = TimeSpan.FromSeconds(30) };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);

            sut.Put(999, new Person("Foo", 10));

            var cacheKey = sut.CacheNamespace.GetKey(999);
            var ttl = Redis.KeyTimeToLive(cacheKey);
            Assert.InRange(ttl.Value, TimeSpan.FromSeconds(29), TimeSpan.FromSeconds(30));
        }

        [Fact]
        void Get_should_deserialize_data()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            sut.Put(999, new Person("Foo", 10));

            var person = sut.Get(999) as Person;

            Assert.NotNull(person);
            Assert.Equal("Foo", person.Name);
            Assert.Equal(10, person.Age);
        }

        [Fact]
        void Get_should_return_null_if_not_exists()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            var person = sut.Get(99999) as Person;

            Assert.Null(person);
        }

        [Fact]
        void Get_after_cache_has_been_cleared_returns_null()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            sut.Put(999, new Person("John Doe", 20));
            sut.Clear();
            var result = sut.Get(999);

            Assert.Null(result);
        }

        [Fact]
        void Get_after_item_has_expired_returns_null()
        {
            var config = new RedisCacheConfiguration("region") { Expiration = TimeSpan.FromMilliseconds(500) };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            sut.Put(1, new Person("John Doe", 20));

            Thread.Sleep(TimeSpan.FromMilliseconds(600));
            var result = sut.Get(1);

            Assert.Null(result);
        }

        [Fact]
        void Get_after_item_has_expired_removes_the_key_from_set_of_all_keys()
        {
            const int key = 1;
            var config = new RedisCacheConfiguration("region")
            {
                Expiration = TimeSpan.FromMilliseconds(500),
                SlidingExpiration = RedisCacheConfiguration.NoSlidingExpiration
            };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            sut.Put(key, new Person("John Doe", 20));

            Thread.Sleep(TimeSpan.FromMilliseconds(600));
            var result = sut.Get(key);

            var setOfActiveKeysKey = sut.CacheNamespace.GetSetOfActiveKeysKey();
            var cacheKey = sut.CacheNamespace.GetKey(key);
            var isKeyStillTracked = Redis.SetContains(setOfActiveKeysKey, cacheKey);
            Assert.False(isKeyStillTracked);
        }

        [Fact]
        void Get_when_sliding_expiration_not_set_does_not_extend_the_expiration()
        {
            var config = new RedisCacheConfiguration("region")
            {
                Expiration = TimeSpan.FromMilliseconds(500),
                SlidingExpiration = RedisCacheConfiguration.NoSlidingExpiration
            };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            sut.Put(1, new Person("John Doe", 10));

            Thread.Sleep(TimeSpan.FromMilliseconds(200));
            var result = sut.Get(1);

            var cacheKey = sut.CacheNamespace.GetKey(1);
            var expiry = Redis.KeyTimeToLive(cacheKey);
            Assert.InRange(expiry.Value, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(300));
        }

        [Fact]
        void Get_when_sliding_expiration_set_and_time_to_live_is_greater_than_expiration_does_not_reset_the_expiration()
        {
            var config = new RedisCacheConfiguration("region")
            {
                Expiration = TimeSpan.FromMilliseconds(500),
                SlidingExpiration = TimeSpan.FromMilliseconds(100)
            };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            sut.Put(1, new Person("John Doe", 10));

            Thread.Sleep(TimeSpan.FromMilliseconds(200));
            var result = sut.Get(1);

            var cacheKey = sut.CacheNamespace.GetKey(1);
            var expiry = Redis.KeyTimeToLive(cacheKey);
            Assert.InRange(expiry.Value, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(300));
        }

        [Fact]
        void Get_when_sliding_expiration_and_time_to_live_is_less_than_expiration_resets_the_expiration()
        {
            var config = new RedisCacheConfiguration("region")
            {
                Expiration = TimeSpan.FromMilliseconds(500),
                SlidingExpiration = TimeSpan.FromMilliseconds(400)
            };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            sut.Put(1, new Person("John Doe", 10));

            Thread.Sleep(TimeSpan.FromMilliseconds(200));
            var result = sut.Get(1);

            var cacheKey = sut.CacheNamespace.GetKey(1);
            var expiry = Redis.KeyTimeToLive(cacheKey);
            Assert.InRange(expiry.Value, TimeSpan.FromMilliseconds(480), TimeSpan.FromMilliseconds(500));
        }
        
        [Fact]
        void Put_and_Get_into_different_cache_regions()
        {
            const int key = 1;
            var sut1 = new RedisCache("region_A", ConnectionMultiplexer, options);
            var sut2 = new RedisCache("region_B", ConnectionMultiplexer, options);

            sut1.Put(key, new Person("A", 1));
            sut2.Put(key, new Person("B", 1));

            Assert.Equal("A", ((Person)sut1.Get(1)).Name);
            Assert.Equal("B", ((Person)sut2.Get(1)).Name);
        }

        [Fact]
        void Remove_should_remove_from_cache()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            sut.Put(999, new Person("Foo", 10));

            sut.Remove(999);

            var result = sut.Get(999);
            Assert.Null(result);
        }

        [Fact]
        void Clear_should_remove_all_items_from_cache()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            sut.Put(1, new Person("A", 1));
            sut.Put(2, new Person("B", 2));
            sut.Put(3, new Person("C", 3));
            sut.Put(4, new Person("D", 4));

            sut.Clear();

            Assert.Null(sut.Get(1));
            Assert.Null(sut.Get(2));
            Assert.Null(sut.Get(3));
            Assert.Null(sut.Get(4));
        }

        [Fact]
        void Destroy_should_not_clear()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            sut.Put(1, new Person("John Doe", 20));

            sut.Destroy();

            var result = sut.Get(1);
            var person = Assert.IsType<Person>(result);
            Assert.Equal("John Doe", person.Name);
            Assert.Equal(20, person.Age);
        }

        [Fact]
        void Lock_and_Unlock_concurrently_with_same_cache_client()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            sut.Put(1, new Person("Foo", 1));

            var results = new ConcurrentQueue<string>();
            const int numberOfClients = 5;

            var tasks = new List<Task>();
            for (var i = 1; i <= numberOfClients; i++)
            {
                int clientNumber = i;
                var t = Task.Factory.StartNew(() =>
                {
                    var key = "1";
                    sut.Lock(key);
                    results.Enqueue(clientNumber + " lock");

                    // Artificial concurrency.
                    Thread.Sleep(100);

                    results.Enqueue(clientNumber + " unlock");
                    sut.Unlock(key);
                });

                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            // Each Lock should be followed by its associated Unlock.
            var listResults = results.ToList();
            for (var i = 1; i <= numberOfClients; i++)
            {
                var lockIndex = listResults.IndexOf(i + " lock");
                Assert.Equal(i + " lock", listResults[lockIndex]);
                Assert.Equal(i + " unlock", listResults[lockIndex + 1]);
            }
        }

        [Fact]
        void Lock_and_Unlock_concurrently_with_different_cache_clients()
        {
            var mainCache = new RedisCache("region", ConnectionMultiplexer, options);
            mainCache.Put(1, new Person("Foo", 1));

            var results = new ConcurrentQueue<string>();
            const int numberOfClients = 5;

            var tasks = new List<Task>();
            for (var i = 1; i <= numberOfClients; i++)
            {
                int clientNumber = i;
                var t = Task.Factory.StartNew(() =>
                {
                    var cacheX = new RedisCache("region", ConnectionMultiplexer, options);
                    var key = "1";
                    cacheX.Lock(key);
                    results.Enqueue(clientNumber + " lock");

                    // Artificial concurrency.
                    Thread.Sleep(100);

                    results.Enqueue(clientNumber + " unlock");
                    cacheX.Unlock(key);
                });

                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            // Each Lock should be followed by its associated Unlock.
            var listResults = results.ToList();
            for (var i = 1; i <= numberOfClients; i++)
            {
                var lockIndex = listResults.IndexOf(i + " lock");
                Assert.Equal(i + " lock", listResults[lockIndex]);
                Assert.Equal(i + " unlock", listResults[lockIndex + 1]);
            }
        }

        [Fact]
        void Unlock_when_not_locally_locked_triggers_the_unlock_failed_event()
        {
            var unlockFailedCounter = 0;
            options.UnlockFailed += (sender, e) =>
            {
                if (e.LockKey == null && e.LockValue == null)
                {
                    unlockFailedCounter++;
                }
            };
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            sut.Unlock(123);

            Assert.Equal(1, unlockFailedCounter);
        }

        [Fact]
        void Unlock_when_locked_locally_but_not_locked_in_redis_triggers_the_unlock_failed_event()
        {
            var unlockFailedCounter = 0;
            options.UnlockFailed += (sender, e) =>
            {
                if (e.LockKey != null && e.LockValue != null)
                {
                    unlockFailedCounter++;
                }
            };
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            const int key = 123;

            sut.Lock(key);
            var lockKey = sut.CacheNamespace.GetLockKey(key);
            Redis.KeyDelete(lockKey);
            sut.Unlock(key);

            Assert.Equal(1, unlockFailedCounter);
        }

        [Fact]
        void Lock_when_failed_to_acquire_lock_triggers_the_unlock_failed_event()
        {
            var lockFailedCounter = 0;
            options.LockFailed += (sender, e) =>
            {
                lockFailedCounter++;
            };
            options.AcquireLockRetryStrategy = new DoNotRetryAcquireLockRetryStrategy();
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            const int key = 123;

            sut.Lock(key);
            sut.Lock(key);

            Assert.Equal(1, lockFailedCounter);
        }
    }
}
