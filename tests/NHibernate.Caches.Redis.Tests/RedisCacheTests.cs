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
        void Constructor_should_set_generation_if_it_does_not_exist()
        {
            var cache = new RedisCache("regionName", ConnectionMultiplexer, options);

            var genKey = cache.CacheNamespace.GetGenerationKey();
            Assert.Contains("NHibernate-Cache:regionName", genKey);
            Assert.Equal(1, cache.CacheNamespace.GetGeneration());
        }

        [Fact]
        void Constructor_should_get_current_generation_if_it_already_exists()
        {
            // Distributed caches.
            var cache1 = new RedisCache("regionName", ConnectionMultiplexer, options);
            var cache2 = new RedisCache("regionName", ConnectionMultiplexer, options);

            Assert.Equal(1, cache1.CacheNamespace.GetGeneration());
            Assert.Equal(1, cache2.CacheNamespace.GetGeneration());
        }

        [Fact]
        void Put_should_serialize_item_and_set_with_expiry()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            sut.Put(999, new Person("Foo", 10));

            var cacheKey = sut.CacheNamespace.GetKey(999);
            var data = Redis.StringGet(cacheKey);
            var expiry = Redis.KeyTimeToLive(cacheKey);

            Assert.InRange(expiry.Value, low: TimeSpan.FromMinutes(4), high: TimeSpan.FromMinutes(5));

            var person = options.Serializer.Deserialize(data) as Person;
            Assert.NotNull(person);
            Assert.Equal("Foo", person.Name);
            Assert.Equal(10, person.Age);
        }

        [Fact]
        void Configure_cache_expiration()
        {
            var configuration = new RedisCacheConfiguration("region", TimeSpan.FromMinutes(99));
            var sut = new RedisCache("region", configuration, ConnectionMultiplexer, options);

            sut.Put(999, new Person("Foo", 10));

            var cacheKey = sut.CacheNamespace.GetKey(999);
            var expiry = Redis.KeyTimeToLive(cacheKey);
            Assert.InRange(expiry.Value, low: TimeSpan.FromMinutes(98), high: TimeSpan.FromMinutes(99));
        }

        [Fact]
        void Configure_cache_lock_timeout()
        {
            var configuration = new RedisCacheConfiguration("region", lockTimeout: TimeSpan.FromSeconds(123));
            var sut = new RedisCache("region", configuration, ConnectionMultiplexer, options);
            const string key = "123";
            
            sut.Lock(key);
            var lockKey = sut.CacheNamespace.GetLockKey(key);

            var expiry = Redis.KeyTimeToLive(lockKey);
            Assert.InRange(expiry.Value, low: TimeSpan.FromSeconds(120), high: TimeSpan.FromSeconds(123));
        }

        [Fact]
        void Put_should_retry_until_generation_matches_the_server()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            // Another client incremented the generation.
            Redis.StringIncrement(sut.CacheNamespace.GetGenerationKey(), 100);

            sut.Put(999, new Person("Foo", 10));

            Assert.Equal(101, sut.CacheNamespace.GetGeneration());
            var data = Redis.StringGet(sut.CacheNamespace.GetKey(999));
            var person = (Person)options.Serializer.Deserialize(data);
            Assert.Equal("Foo", person.Name);
            Assert.Equal(10, person.Age);
        }

        [Fact]
        void Put_should_retry_until_generation_matches_server_when_generation_is_cleared()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            // Someone cleared the generation (or it doesn't exist yet).
            Redis.KeyDelete(sut.CacheNamespace.GetGenerationKey());

            sut.Put(999, new Person("Foo", 10));

            Assert.Equal(1, sut.CacheNamespace.GetGeneration());
            var serverGeneration = Redis.StringGet(sut.CacheNamespace.GetGenerationKey());
            Assert.Equal(1, serverGeneration);

            var data = Redis.StringGet(sut.CacheNamespace.GetKey(999));
            var person = (Person)options.Serializer.Deserialize(data);
            Assert.Equal("Foo", person.Name);
            Assert.Equal(10, person.Age);
        }

        [Fact]
        void Put_should_retry_until_generation_matches_server_when_generation_is_greater_than_server_generation()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            // Someone has set a lower generation.
            Redis.StringSet(sut.CacheNamespace.GetGenerationKey(), 0);

            sut.Put(999, new Person("Foo", 10));

            Assert.Equal(1, sut.CacheNamespace.GetGeneration());
            var serverGeneration = Redis.StringGet(sut.CacheNamespace.GetGenerationKey());
            Assert.Equal(1, serverGeneration);

            var data = Redis.StringGet(sut.CacheNamespace.GetKey(999));
            var person = (Person)options.Serializer.Deserialize(data);
            Assert.Equal("Foo", person.Name);
            Assert.Equal(10, person.Age);
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
        void Get_should_retry_until_generation_matches_the_server()
        {
            var sut1 = new RedisCache("region", ConnectionMultiplexer, options);

            // Another client incremented the generation.
            Redis.StringIncrement(sut1.CacheNamespace.GetGenerationKey(), 100);
            var sut2 = new RedisCache("region", ConnectionMultiplexer, options);
            sut2.Put(999, new Person("Foo", 10));

            var person = sut1.Get(999) as Person;

            Assert.Equal(101, sut1.CacheNamespace.GetGeneration());
            Assert.NotNull(person);
            Assert.Equal("Foo", person.Name);
            Assert.Equal(10, person.Age);
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

            var result = Redis.StringGet(sut.CacheNamespace.GetKey(999));
            Assert.False(result.HasValue);
        }

        [Fact]
        void Remove_should_retry_until_generation_matches_the_server()
        {
            var sut1 = new RedisCache("region", ConnectionMultiplexer, options);

            // Another client incremented the generation.
            Redis.StringIncrement(sut1.CacheNamespace.GetGenerationKey(), 100);
            var sut2 = new RedisCache("region", ConnectionMultiplexer, options);
            sut2.Put(999, new Person("Foo", 10));

            sut1.Remove(999);

            Assert.Equal(101, sut1.CacheNamespace.GetGeneration());
            var result = Redis.StringGet(sut1.CacheNamespace.GetKey(999));
            Assert.False(result.HasValue);
        }

        [Fact]
        void Clear_update_generation_and_clear_keys_for_this_region()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);
            sut.Put(1, new Person("Foo", 1));
            sut.Put(2, new Person("Bar", 2));
            sut.Put(3, new Person("Baz", 3));
            var oldKey1 = sut.CacheNamespace.GetKey(1);
            var oldKey2 = sut.CacheNamespace.GetKey(2);
            var oldKey3 = sut.CacheNamespace.GetKey(3);

            var setOfKeysKey = sut.CacheNamespace.GetSetOfKeysKey();

            sut.Clear();

            // New generation.
            Assert.Equal(2, sut.CacheNamespace.GetGeneration());
            Assert.False(Redis.StringGet(sut.CacheNamespace.GetKey(1)).HasValue);
            Assert.False(Redis.StringGet(sut.CacheNamespace.GetKey(2)).HasValue);
            Assert.False(Redis.StringGet(sut.CacheNamespace.GetKey(3)).HasValue);
            
            // List of keys for this region was cleared.
            Assert.False(Redis.StringGet(setOfKeysKey).HasValue);

            // The old values will expire automatically.
            var ttl1 = Redis.KeyTimeToLive(oldKey1);
            Assert.True(ttl1 <= TimeSpan.FromMinutes(5));
            var ttl2 = Redis.KeyTimeToLive(oldKey2);
            Assert.True(ttl2 <= TimeSpan.FromMinutes(5));
            var ttl3 = Redis.KeyTimeToLive(oldKey3);
            Assert.True(ttl3 <= TimeSpan.FromMinutes(5));
        }

        [Fact]
        void Clear_should_ensure_generation_if_another_cache_has_already_incremented_the_generation()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            // Another cache updated its generation (by clearing).
            Redis.StringSet(sut.CacheNamespace.GetGenerationKey(), 100);

            sut.Clear();

            Assert.Equal(101, sut.CacheNamespace.GetGeneration());
        }

        [Fact]
        void Destroy_should_not_clear()
        {
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            sut.Destroy();

            Assert.Equal(1, sut.CacheNamespace.GetGeneration());
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
        void Unlock_when_not_locally_locked_triggers_event()
        {
            var unlockFailedCounter = 0;
            options.OnUnlockFailed = e =>
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
        void Unlock_when_locked_locally_but_not_locked_in_redis_triggers_event()
        {
            var unlockFailedCounter = 0;
            options.OnUnlockFailed = e =>
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
        void Should_update_server_generation_when_server_has_less_generation_than_the_client()
        {
            const int key = 1;
            var sut = new RedisCache("region", ConnectionMultiplexer, options);

            sut.Put(key, new Person("A", 1));
            FlushDb();
            sut.Put(key, new Person("B", 2));

            var generationKey = sut.CacheNamespace.GetGenerationKey();
            Assert.Equal(sut.CacheNamespace.GetGeneration().ToString(CultureInfo.InvariantCulture), (string)Redis.StringGet(generationKey));
        }
    }
}
