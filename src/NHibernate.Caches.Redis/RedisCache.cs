using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Cache;
using NHibernate.Util;
using ServiceStack.Common;
using ServiceStack.Redis;
using ServiceStack.Redis.Support;
using ServiceStack.Text;

namespace NHibernate.Caches.Redis
{
    public class RedisCache : ICache
    {
        private const string CacheNamePrefix = "NHibernate-Cache:";

        private static readonly IInternalLogger log;

        private readonly Dictionary<object, string> acquiredLocks = new Dictionary<object, string>();
        private readonly ISerializer serializer;
        private readonly IRedisClientsManager clientManager;
        private readonly int expirySeconds;
        private readonly TimeSpan lockTimeout = TimeSpan.FromSeconds(30);

        private const int DefaultExpiry = 300 /*5 minutes*/;

        public string RegionName { get; private set; }
        public RedisNamespace CacheNamespace { get; private set; }
        public int Timeout { get { return Timestamper.OneMs * 60000; } }

        static RedisCache()
        {
            log = LoggerProvider.LoggerFor(typeof(RedisCache));
        }

        public RedisCache(string regionName, IRedisClientsManager clientManager)
            : this(regionName, new Dictionary<string, string>(), null, clientManager)
        {

        }

        public RedisCache(string regionName, IDictionary<string, string> properties, RedisCacheElement element, IRedisClientsManager clientManager)
        {
            this.serializer = new ObjectSerializer();
            this.clientManager = clientManager.ThrowIfNull("clientManager");
            this.RegionName = regionName.ThrowIfNull("regionName");

            this.expirySeconds = element != null 
                ? (int)element.Expiration.TotalSeconds
                : PropertiesHelper.GetInt32(Cfg.Environment.CacheDefaultExpiration, properties, DefaultExpiry);

            log.DebugFormat("using expiration : {0} seconds", this.expirySeconds);

            var regionPrefix = PropertiesHelper.GetString(Cfg.Environment.CacheRegionPrefix, properties, null);
            log.DebugFormat("using region prefix : {0}", regionPrefix);

            var namespacePrefix = CacheNamePrefix + this.RegionName;
            if (!String.IsNullOrWhiteSpace(regionPrefix))
            {
                namespacePrefix = regionPrefix + ":" + namespacePrefix;
            }

            this.CacheNamespace = new RedisNamespace(namespacePrefix);
            this.SyncGeneration();
        }

        public long NextTimestamp()
        {
            return Timestamper.Next();
        }

        private void SyncGeneration()
        {
            if (CacheNamespace.GetGeneration() == -1)
            {
                CacheNamespace.SetGeneration(FetchGeneration());
            }
        }

        private long FetchGeneration()
        {
            using (var client = this.clientManager.GetClient())
            {
                var generationKey = CacheNamespace.GetGenerationKey();
                var attemptedGeneration = client.GetValue(generationKey);

                if (attemptedGeneration == null)
                {
                    var generation = client.Increment(generationKey, 1);
                    log.DebugFormat("creating new generation : {0}", generation);
                    return generation;
                }
                else
                {
                    log.DebugFormat("using existing generation : {0}", attemptedGeneration);
                    return Convert.ToInt64(attemptedGeneration);
                }
            }
        }

        public void Put(object key, object value)
        {
            key.ThrowIfNull("key");
            value.ThrowIfNull("value");

            log.DebugFormat("put in cache : {0}", key);

            try
            {
                var data = serializer.Serialize(value);

                ExecuteEnsureGeneration(transaction =>
                {
                    transaction.QueueCommand(r =>
                    {
                        var cacheKey = CacheNamespace.GlobalCacheKey(key);
                        ((IRedisNativeClient)r).SetEx(cacheKey, expirySeconds, data);
                    });

                    transaction.QueueCommand(r =>
                    {
                        var globalKeysKey = CacheNamespace.GetGlobalKeysKey();
                        var cacheKey = CacheNamespace.GlobalCacheKey(key);
                        r.AddItemToSet(globalKeysKey, cacheKey);
                    });
                });
            }
            catch (Exception)
            {
                log.WarnFormat("could not put in cache : {0}", key);
                throw;
            }
        }

        public object Get(object key)
        {
            key.ThrowIfNull();

            log.DebugFormat("get from cache : {0}", key);

            try
            {
                byte[] data = null;

                ExecuteEnsureGeneration(transaction =>
                {
                    transaction.QueueCommand(r =>
                    {
                        var cacheKey = CacheNamespace.GlobalCacheKey(key);
                        return ((IRedisNativeClient)r).Get(cacheKey);
                    }, x => data = x);
                });
                
                return serializer.Deserialize(data);

            }
            catch (Exception)
            {
                log.WarnFormat("coult not get from cache : {0}", key);
                throw;
            }
        }

        public void Remove(object key)
        {
            key.ThrowIfNull();

            log.DebugFormat("remove from cache : {0}", key);

            try
            {
                ExecuteEnsureGeneration(transaction =>
                {
                    transaction.QueueCommand(r =>
                    {
                        var cacheKey = CacheNamespace.GlobalCacheKey(key);
                        ((RedisNativeClient)r).Del(cacheKey);
                    });
                });
            }
            catch (Exception)
            {
                log.WarnFormat("could not remove from cache : {0}", key);
                throw;
            }
        }

        public void Clear()
        {
            var generationKey = CacheNamespace.GetGenerationKey();
            var globalKeysKey = CacheNamespace.GetGlobalKeysKey();

            log.DebugFormat("clear cache : {0}", generationKey);

            try
            {
                using (var client = this.clientManager.GetClient())
                using (var transaction = client.CreateTransaction())
                {
                    // Update to a new generation.
                    transaction.QueueCommand(r =>
                        r.Increment(generationKey, 1),
                        x => CacheNamespace.SetGeneration(x));

                    // Empty the set of current keys for this region.
                    // NOTE: The actual cached objects will eventually expire.
                    transaction.QueueCommand(
                        r => r.Remove(globalKeysKey));

                    transaction.Commit();
                }
            }
            catch (Exception)
            {
                log.WarnFormat("could not clear cache : {0}", generationKey);
                throw;
            }
        }

        public void Destroy()
        {
            // No-op since Redis is distributed.
            log.DebugFormat("destroying cache : {0}", this.CacheNamespace.GetGenerationKey());
        }

        public void Lock(object key)
        {
            log.DebugFormat("acquiring cache lock : {0}", key);

            try
            {
                var globalKey = CacheNamespace.GlobalKey(key, RedisNamespace.NumTagsForLockKey);

                using (var client = this.clientManager.GetClient())
                {
                    // Derived from ServiceStack's RedisLock.
                    ExecExtensions.RetryUntilTrue(() => 
                    {
                        var wasSet = client.SetEntryIfNotExists(globalKey, "lock " + DateTime.UtcNow.ToUnixTime());

                        if (wasSet)
                        {
                            acquiredLocks[key] = globalKey;
                        }

                        return wasSet;
                    }, lockTimeout);
                }
            }
            catch (Exception)
            {
                log.WarnFormat("could not acquire cache lock : ", key);
                throw;
            }
        }

        public void Unlock(object key)
        {
            string globalKey;
            if (!acquiredLocks.TryGetValue(key, out globalKey)) { return; }

            log.DebugFormat("releasing cache lock : {0}", key);

            try
            {
                using (var client = this.clientManager.GetClient())
                {
                    client.Remove(globalKey);
                }
            }
            catch (Exception)
            {
                log.WarnFormat("could not release cache lock : {0}", key);
                throw;
            }
        }

        private void ExecuteEnsureGeneration(Action<IRedisTransaction> action)
        {
            long serverGeneration = -1;

            using (var client = this.clientManager.GetClient())
            using (var transaction = client.CreateTransaction())
            {
                action(transaction);

                transaction.QueueCommand(r =>
                    r.GetValue(CacheNamespace.GetGenerationKey()),
                    x => serverGeneration = Convert.ToInt64(x));

                transaction.Commit();

                // Another client/cache has changed the generation. Therefore, 
                // this cache is out of date so we need to sync the generation 
                // with the server.
                while (serverGeneration != CacheNamespace.GetGeneration())
                {
                    CacheNamespace.SetGeneration(serverGeneration);
                    transaction.Replay();
                }
            }
        }
    }
}