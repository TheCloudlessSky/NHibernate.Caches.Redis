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
    public class AsyncRedisCacheTests : RedisTest
    {
        private readonly RedisCacheProviderOptions options;

        public AsyncRedisCacheTests()
        {
            options = CreateTestProviderOptions();
        }

       

        [Fact]
        async Task Put_adds_the_item_to_the_cache()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            await sut.PutAsync(999, new Person("Foo", 10), CancellationToken.None);

            var cacheKey = sut.CacheNamespace.GetKey(999);
            var data = await Redis.StringGetAsync(cacheKey);
            var person = (Person)options.Serializer.Deserialize(data);
            Assert.Equal("Foo", person.Name);
            Assert.Equal(10, person.Age);
        }

        [Fact]
        async Task Put_sets_an_expiration_on_the_item()
        {
            var config = new RedisCacheConfiguration("region") { Expiration = TimeSpan.FromSeconds(30) };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);

            await sut.PutAsync(999, new Person("Foo", 10), CancellationToken.None);

            var cacheKey = sut.CacheNamespace.GetKey(999);
            var ttl = await Redis.KeyTimeToLiveAsync(cacheKey);
            Assert.InRange(ttl.Value, TimeSpan.FromSeconds(29), TimeSpan.FromSeconds(30));
        }

        [Fact]
        async Task Get_should_deserialize_data()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            await sut.PutAsync(999, new Person("Foo", 10), CancellationToken.None);

            var person = await sut.GetAsync(999, CancellationToken.None) as Person;

            Assert.NotNull(person);
            Assert.Equal("Foo", person.Name);
            Assert.Equal(10, person.Age);
        }

        [Fact]
        async Task Get_should_return_null_if_not_exists()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            var person = await sut.GetAsync(99999, CancellationToken.None) as Person;

            Assert.Null(person);
        }

        [Fact]
        async Task Get_after_cache_has_been_cleared_returns_null()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            await sut.PutAsync(999, new Person("John Doe", 20), CancellationToken.None);
            await sut.ClearAsync(CancellationToken.None);
            var result = await sut.GetAsync(999, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        async Task Get_after_item_has_expired_returns_null()
        {
            var config = new RedisCacheConfiguration("region") { Expiration = TimeSpan.FromMilliseconds(500) };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            await sut.PutAsync(1, new Person("John Doe", 20), CancellationToken.None);

            await Task.Delay(TimeSpan.FromMilliseconds(600));
            var result = await sut.GetAsync(1, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        async Task Get_after_item_has_expired_removes_the_key_from_set_of_all_keys()
        {
            const int key = 1;
            var config = new RedisCacheConfiguration("region")
            {
                Expiration = TimeSpan.FromMilliseconds(500),
                SlidingExpiration = RedisCacheConfiguration.NoSlidingExpiration
            };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            await sut.PutAsync(key, new Person("John Doe", 20), CancellationToken.None);

            await Task.Delay(TimeSpan.FromMilliseconds(600));
            var result = await sut.GetAsync(key, CancellationToken.None);

            var setOfActiveKeysKey = sut.CacheNamespace.GetSetOfActiveKeysKey();
            var cacheKey = sut.CacheNamespace.GetKey(key);
            var isKeyStillTracked = await Redis.SetContainsAsync(setOfActiveKeysKey, cacheKey);
            Assert.False(isKeyStillTracked);
        }

        [Fact]
        async Task Get_when_sliding_expiration_not_set_does_not_extend_the_expiration()
        {
            var config = new RedisCacheConfiguration("region")
            {
                Expiration = TimeSpan.FromMilliseconds(500),
                SlidingExpiration = RedisCacheConfiguration.NoSlidingExpiration
            };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            await sut.PutAsync(1, new Person("John Doe", 10), CancellationToken.None);

            await Task.Delay(TimeSpan.FromMilliseconds(200));
            var result = await sut.GetAsync(1, CancellationToken.None);

            var cacheKey = sut.CacheNamespace.GetKey(1);
            var expiry = await Redis.KeyTimeToLiveAsync(cacheKey);
            Assert.InRange(expiry.Value, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(300));
        }

        [Fact]
        async Task Get_when_sliding_expiration_set_and_time_to_live_is_greater_than_expiration_does_not_reset_the_expiration()
        {
            var config = new RedisCacheConfiguration("region")
            {
                Expiration = TimeSpan.FromMilliseconds(500),
                SlidingExpiration = TimeSpan.FromMilliseconds(100)
            };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            await sut.PutAsync(1, new Person("John Doe", 10), CancellationToken.None);

            await Task.Delay(TimeSpan.FromMilliseconds(200));
            var result = await sut.GetAsync(1, CancellationToken.None);

            var cacheKey = sut.CacheNamespace.GetKey(1);
            var expiry = await Redis.KeyTimeToLiveAsync(cacheKey);
            Assert.InRange(expiry.Value, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(300));
        }

        [Fact]
        async Task Get_when_sliding_expiration_and_time_to_live_is_less_than_expiration_resets_the_expiration()
        {
            var config = new RedisCacheConfiguration("region")
            {
                Expiration = TimeSpan.FromMilliseconds(500),
                SlidingExpiration = TimeSpan.FromMilliseconds(400)
            };
            var sut = new RedisCache(config, ConnectionMultiplexer, options);
            await sut.PutAsync(1, new Person("John Doe", 10), CancellationToken.None);

            await Task.Delay(TimeSpan.FromMilliseconds(200));
            var result = await sut.GetAsync(1, CancellationToken.None);

            var cacheKey = sut.CacheNamespace.GetKey(1);
            var expiry = await Redis.KeyTimeToLiveAsync(cacheKey);
            Assert.InRange(expiry.Value, TimeSpan.FromMilliseconds(480), TimeSpan.FromMilliseconds(500));
        }
        
        [Fact]
        async Task Put_and_Get_into_different_cache_regions()
        {
            const int key = 1;
            var sut1 = new RedisCache("region_A", ConnectionMultiplexer, options);
            var sut2 = new RedisCache("region_B", ConnectionMultiplexer, options);

            await sut1.PutAsync(key, new Person("A", 1), CancellationToken.None);
            await sut2.PutAsync(key, new Person("B", 1), CancellationToken.None);

            Assert.Equal("A", ((Person) await sut1.GetAsync(1, CancellationToken.None)).Name);
            Assert.Equal("B", ((Person) await sut2.GetAsync(1, CancellationToken.None)).Name);
        }

        [Fact]
        async Task Remove_should_remove_from_cache()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            await sut.PutAsync(999, new Person("Foo", 10), CancellationToken.None);

            await sut.RemoveAsync(999, CancellationToken.None);

            var result = await sut.GetAsync(999, CancellationToken.None);
            Assert.Null(result);
        }

        [Fact]
        async Task Clear_should_remove_all_items_from_cache()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            await sut.PutAsync(1, new Person("A", 1), CancellationToken.None);
            await sut.PutAsync(2, new Person("B", 2), CancellationToken.None);
            await sut.PutAsync(3, new Person("C", 3), CancellationToken.None);
            await sut.PutAsync(4, new Person("D", 4), CancellationToken.None);

            await sut.ClearAsync(CancellationToken.None);

            Assert.Null(await sut.GetAsync(1, CancellationToken.None));
            Assert.Null(await sut.GetAsync(2, CancellationToken.None));
            Assert.Null(await sut.GetAsync(3, CancellationToken.None));
            Assert.Null(await sut.GetAsync(4, CancellationToken.None));
        }

        

        [Fact]
        async Task Lock_and_Unlock_concurrently_with_same_cache_client()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            await sut.PutAsync(1, new Person("Foo", 1), CancellationToken.None);

            var results = new ConcurrentQueue<string>();
            const int numberOfClients = 5;

            var tasks = new List<Task>();
            for (var i = 1; i <= numberOfClients; i++)
            {
                int clientNumber = i;
                var t = Task.Run(async () =>
                {
                    var key = "1";
                    await sut.LockAsync(key, CancellationToken.None);
                    results.Enqueue(clientNumber + " lock");

                    // Artificial concurrency.
                    await Task.Delay(100);

                    results.Enqueue(clientNumber + " unlock");
                    await sut.UnlockAsync(key, CancellationToken.None);
                });

                tasks.Add(t);
            }

            await Task.WhenAll(tasks.ToArray());

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
        async Task Lock_and_Unlock_concurrently_with_different_cache_clients()
        {
            var mainCache = new RedisCache("region", ConnectionMultiplexer, options);
            await mainCache.PutAsync(1, new Person("Foo", 1), CancellationToken.None);

            var results = new ConcurrentQueue<string>();
            const int numberOfClients = 5;

            var tasks = new List<Task>();
            for (var i = 1; i <= numberOfClients; i++)
            {
                int clientNumber = i;
                var t = Task.Run(async () =>
                {
                    var cacheX = new RedisCache("region", ConnectionMultiplexer, options);
                    var key = "1";
                    await cacheX.LockAsync(key, CancellationToken.None);
                    results.Enqueue(clientNumber + " lock");

                    // Artificial concurrency.
                    await Task.Delay(100);

                    results.Enqueue(clientNumber + " unlock");
                    await cacheX.UnlockAsync(key, CancellationToken.None);
                });

                tasks.Add(t);
            }

            await Task.WhenAll(tasks.ToArray());

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
        async Task Unlock_when_not_locally_locked_triggers_the_unlock_failed_event()
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

            await sut.UnlockAsync(123, CancellationToken.None);

            Assert.Equal(1, unlockFailedCounter);
        }

        [Fact]
        async Task Unlock_when_locked_locally_but_not_locked_in_redis_triggers_the_unlock_failed_event()
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

            await sut.LockAsync(key, CancellationToken.None);
            var lockKey = sut.CacheNamespace.GetLockKey(key);
            await Redis.KeyDeleteAsync(lockKey);
            await sut.UnlockAsync(key, CancellationToken.None);

            Assert.Equal(1, unlockFailedCounter);
        }

        [Fact]
        async Task Lock_when_failed_to_acquire_lock_triggers_the_unlock_failed_event()
        {
            var lockFailedCounter = 0;
            options.LockFailed += (sender, e) =>
            {
                lockFailedCounter++;
            };
            options.AcquireLockRetryStrategy = new DoNotRetryAcquireLockRetryStrategy();
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            const int key = 123;

            await sut.LockAsync(key, CancellationToken.None);
            await sut.LockAsync(key, CancellationToken.None);

            Assert.Equal(1, lockFailedCounter);
        }
    }
}
