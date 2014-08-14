using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NHibernate.Cache;
using NHibernate.Util;
using System.Net.Sockets;
using StackExchange.Redis;

namespace NHibernate.Caches.Redis
{
    public class RedisCache : ICache
    {
        private const string CacheNamePrefix = "NHibernate-Cache:";

        private static readonly IInternalLogger log = LoggerProvider.LoggerFor(typeof(RedisCache));

        private readonly Dictionary<object, string> acquiredLocks = new Dictionary<object, string>();
        private readonly ConnectionMultiplexer connectionMultiplexer;
        private readonly RedisCacheProviderOptions options;
        private readonly int expirySeconds;
        private readonly TimeSpan lockTimeout = TimeSpan.FromSeconds(30);

        private const int DefaultExpiry = 300 /*5 minutes*/;

        public string RegionName { get; private set; }
        public RedisNamespace CacheNamespace { get; private set; }
        public int Timeout { get { return Timestamper.OneMs * 60000; } }

        public RedisCache(string regionName, ConnectionMultiplexer clientManager, RedisCacheProviderOptions options)
            : this(regionName, new Dictionary<string, string>(), null, clientManager, options)
        {

        }

        public RedisCache(string regionName, IDictionary<string, string> properties, RedisCacheElement element, ConnectionMultiplexer connectionMultiplexer, RedisCacheProviderOptions options)
        {
            this.connectionMultiplexer = connectionMultiplexer.ThrowIfNull("connectionMultiplexer");
            this.options = options.ThrowIfNull("options").Clone();

            RegionName = regionName.ThrowIfNull("regionName");

            expirySeconds = element != null
                ? (int)element.Expiration.TotalSeconds
                : PropertiesHelper.GetInt32(Cfg.Environment.CacheDefaultExpiration, properties, DefaultExpiry);

            log.DebugFormat("using expiration : {0} seconds", expirySeconds);

            var regionPrefix = PropertiesHelper.GetString(Cfg.Environment.CacheRegionPrefix, properties, null);
            log.DebugFormat("using region prefix : {0}", regionPrefix);

            var namespacePrefix = CacheNamePrefix + RegionName;
            if (!String.IsNullOrWhiteSpace(regionPrefix))
            {
                namespacePrefix = regionPrefix + ":" + namespacePrefix;
            }

            CacheNamespace = new RedisNamespace(namespacePrefix);
            SyncGeneration();
        }

        public long NextTimestamp()
        {
            return Timestamper.Next();
        }

        protected void SyncGeneration()
        {
            try
            {
                if (CacheNamespace.GetGeneration() == -1)
                {
                    CacheNamespace.SetGeneration(FetchGeneration());
                }
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not sync generation");

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        private long FetchGeneration()
        {
            var client = connectionMultiplexer.GetDatabase();

            var generationKey = CacheNamespace.GetGenerationKey();
            var attemptedGeneration = client.StringGet(generationKey);

            if (!attemptedGeneration.HasValue)
            {
                var generation = client.StringIncrement(generationKey);
                log.DebugFormat("creating new generation : {0}", generation);
                return generation;
            }

            log.DebugFormat("using existing generation : {0}", attemptedGeneration);
            return Convert.ToInt64(attemptedGeneration);
        }

        public virtual void Put(object key, object value)
        {
            key.ThrowIfNull("key");
            value.ThrowIfNull("value");

            log.DebugFormat("put in cache : {0}", key);

            try
            {
                var data = options.Serializer.Serialize(value);

                ExecuteEnsureGeneration(transaction =>
                {
                    var cacheKey = CacheNamespace.GlobalCacheKey(key);

                    transaction.StringSetAsync(cacheKey, data, TimeSpan.FromSeconds(expirySeconds));
                    var globalKeysKey = CacheNamespace.GetGlobalKeysKey();

                    transaction.SetAddAsync(globalKeysKey, cacheKey);
                });
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not put in cache : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        public virtual object Get(object key)
        {
            key.ThrowIfNull();

            log.DebugFormat("get from cache : {0}", key);

            try
            {
                Task<RedisValue> dataResult = null;

                ExecuteEnsureGeneration(transaction =>
                {
                    var cacheKey = CacheNamespace.GlobalCacheKey(key);
                    dataResult = transaction.StringGetAsync(cacheKey);
                });

                byte[] data = null;

                if (dataResult != null)
                    data = dataResult.Result;

                return options.Serializer.Deserialize(data);

            }
            catch (Exception e)
            {
                log.ErrorFormat("coult not get from cache : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }

                return null;
            }
        }

        public virtual void Remove(object key)
        {
            key.ThrowIfNull();

            log.DebugFormat("remove from cache : {0}", key);

            try
            {
                ExecuteEnsureGeneration(transaction =>
                {
                    var cacheKey = CacheNamespace.GlobalCacheKey(key);

                    transaction.KeyDeleteAsync(cacheKey);
                });
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not remove from cache : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        public virtual void Clear()
        {
            var generationKey = CacheNamespace.GetGenerationKey();
            var globalKeysKey = CacheNamespace.GetGlobalKeysKey();

            log.DebugFormat("clear cache : {0}", generationKey);

            try
            {
                var client = connectionMultiplexer.GetDatabase();
                var transaction = client.CreateTransaction();

                var generationIncrementResult = transaction.StringIncrementAsync(generationKey);

                transaction.KeyDeleteAsync(globalKeysKey);

                transaction.Execute();

                CacheNamespace.SetGeneration(generationIncrementResult.Result);
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not clear cache : {0}", generationKey);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        public virtual void Destroy()
        {
            // No-op since Redis is distributed.
            log.DebugFormat("destroying cache : {0}", CacheNamespace.GetGenerationKey());
        }

        public virtual void Lock(object key)
        {
            log.DebugFormat("acquiring cache lock : {0}", key);

            try
            {
                var globalKey = CacheNamespace.GlobalKey(key, RedisNamespace.NumTagsForLockKey);

                var client = connectionMultiplexer.GetDatabase();

                ExecExtensions.RetryUntilTrue(() =>
                {
                    var wasSet = client.StringSet(globalKey, "lock " + DateTime.UtcNow.ToUnixTime(), when: When.NotExists);

                    if (wasSet)
                        acquiredLocks[key] = globalKey;

                    return wasSet;
                }, lockTimeout);
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not acquire cache lock : ", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        public virtual void Unlock(object key)
        {
            string globalKey;
            if (!acquiredLocks.TryGetValue(key, out globalKey)) { return; }

            log.DebugFormat("releasing cache lock : {0}", key);

            try
            {
                var client = connectionMultiplexer.GetDatabase();

                client.KeyDelete(globalKey);
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not release cache lock : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        private void ExecuteEnsureGeneration(Action<StackExchange.Redis.ITransaction> action)
        {
            var client = connectionMultiplexer.GetDatabase();

            var executed = false;

            while (!executed)
            {
                var generation = client.StringGet(CacheNamespace.GetGenerationKey());
                var serverGeneration = Convert.ToInt64(generation);

                CacheNamespace.SetGeneration(serverGeneration);

                var transaction = client.CreateTransaction();

                // The generation on the server may have been removed.
                if (serverGeneration < CacheNamespace.GetGeneration())
                {
                    client.StringSetAsync(CacheNamespace.GetGenerationKey(), CacheNamespace.GetGeneration().ToString(CultureInfo.InvariantCulture));
                }

                transaction.AddCondition(Condition.StringEqual(CacheNamespace.GetGenerationKey(), CacheNamespace.GetGeneration()));

                action(transaction);

                executed = transaction.Execute();
            }
        }

        protected virtual void OnException(RedisCacheExceptionEventArgs e)
        {
            var isSocketException = e.Exception is RedisConnectionException || e.Exception is SocketException || e.Exception.InnerException is SocketException;

            if (!isSocketException)
            {
                e.Throw = true;
            }
        }
    }
}