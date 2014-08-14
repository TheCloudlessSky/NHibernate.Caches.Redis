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

        private static readonly IInternalLogger Log = LoggerProvider.LoggerFor(typeof(RedisCache));

        private readonly Dictionary<object, string> _acquiredLocks = new Dictionary<object, string>();
        private readonly ISerializer _serializer;
        private readonly ConnectionMultiplexer _clientManager;
        private readonly int _expirySeconds;
        private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);

        private const int DefaultExpiry = 300 /*5 minutes*/;

        public string RegionName { get; private set; }
        public RedisNamespace CacheNamespace { get; private set; }
        public int Timeout { get { return Timestamper.OneMs * 60000; } }

        public RedisCache(string regionName, ConnectionMultiplexer clientManager)
            : this(regionName, new Dictionary<string, string>(), null, clientManager)
        {

        }

        public RedisCache(string regionName, IDictionary<string, string> properties, RedisCacheElement element, ConnectionMultiplexer clientManager)
        {
            _serializer = new ObjectSerializer();
            _clientManager = clientManager.ThrowIfNull("clientManager");
            RegionName = regionName.ThrowIfNull("regionName");

            _expirySeconds = element != null
                ? (int)element.Expiration.TotalSeconds
                : PropertiesHelper.GetInt32(Cfg.Environment.CacheDefaultExpiration, properties, DefaultExpiry);

            Log.DebugFormat("using expiration : {0} seconds", _expirySeconds);

            var regionPrefix = PropertiesHelper.GetString(Cfg.Environment.CacheRegionPrefix, properties, null);
            Log.DebugFormat("using region prefix : {0}", regionPrefix);

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
                Log.ErrorFormat("could not sync generation");

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        private long FetchGeneration()
        {
            var client = _clientManager.GetDatabase();

            var generationKey = CacheNamespace.GetGenerationKey();
            var attemptedGeneration = client.StringGet(generationKey);

            if (!attemptedGeneration.HasValue)
            {
                var generation = client.StringIncrement(generationKey);
                Log.DebugFormat("creating new generation : {0}", generation);
                return generation;
            }

            Log.DebugFormat("using existing generation : {0}", attemptedGeneration);
            return Convert.ToInt64(attemptedGeneration);
        }

        public virtual void Put(object key, object value)
        {
            key.ThrowIfNull("key");
            value.ThrowIfNull("value");

            Log.DebugFormat("put in cache : {0}", key);

            try
            {
                var data = _serializer.Serialize(value);

                ExecuteEnsureGeneration(transaction =>
                {
                    var cacheKey = CacheNamespace.GlobalCacheKey(key);

                    transaction.StringSetAsync(cacheKey, data, TimeSpan.FromSeconds(_expirySeconds));
                    var globalKeysKey = CacheNamespace.GetGlobalKeysKey();

                    transaction.SetAddAsync(globalKeysKey, cacheKey);
                });
            }
            catch (Exception e)
            {
                Log.ErrorFormat("could not put in cache : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        public virtual object Get(object key)
        {
            key.ThrowIfNull();

            Log.DebugFormat("get from cache : {0}", key);

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

                return _serializer.Deserialize(data);

            }
            catch (Exception e)
            {
                Log.ErrorFormat("coult not get from cache : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }

                return null;
            }
        }

        public virtual void Remove(object key)
        {
            key.ThrowIfNull();

            Log.DebugFormat("remove from cache : {0}", key);

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
                Log.ErrorFormat("could not remove from cache : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        public virtual void Clear()
        {
            var generationKey = CacheNamespace.GetGenerationKey();
            var globalKeysKey = CacheNamespace.GetGlobalKeysKey();

            Log.DebugFormat("clear cache : {0}", generationKey);

            try
            {
                var client = _clientManager.GetDatabase();
                var transaction = client.CreateTransaction();

                var generationIncrementResult = transaction.StringIncrementAsync(generationKey);

                transaction.KeyDeleteAsync(globalKeysKey);

                transaction.Execute();

                CacheNamespace.SetGeneration(generationIncrementResult.Result);
            }
            catch (Exception e)
            {
                Log.ErrorFormat("could not clear cache : {0}", generationKey);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        public virtual void Destroy()
        {
            // No-op since Redis is distributed.
            Log.DebugFormat("destroying cache : {0}", CacheNamespace.GetGenerationKey());
        }

        public virtual void Lock(object key)
        {
            Log.DebugFormat("acquiring cache lock : {0}", key);

            try
            {
                var globalKey = CacheNamespace.GlobalKey(key, RedisNamespace.NumTagsForLockKey);

                var client = _clientManager.GetDatabase();

                ExecExtensions.RetryUntilTrue(() =>
                {
                    var wasSet = client.StringSet(globalKey, "lock " + DateTime.UtcNow.ToUnixTime(), when: When.NotExists);

                    if (wasSet)
                        _acquiredLocks[key] = globalKey;

                    return wasSet;
                }, _lockTimeout);
            }
            catch (Exception e)
            {
                Log.ErrorFormat("could not acquire cache lock : ", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        public virtual void Unlock(object key)
        {
            string globalKey;
            if (!_acquiredLocks.TryGetValue(key, out globalKey)) { return; }

            Log.DebugFormat("releasing cache lock : {0}", key);

            try
            {
                var client = _clientManager.GetDatabase();

                client.KeyDelete(globalKey);
            }
            catch (Exception e)
            {
                Log.ErrorFormat("could not release cache lock : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) { throw; }
            }
        }

        private void ExecuteEnsureGeneration(Action<StackExchange.Redis.ITransaction> action)
        {
            var client = _clientManager.GetDatabase();

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