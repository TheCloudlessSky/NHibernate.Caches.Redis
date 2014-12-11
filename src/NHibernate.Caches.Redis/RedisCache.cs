using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NHibernate.Cache;
using NHibernate.Util;
using System.Net.Sockets;
using StackExchange.Redis;
using System.Runtime.Caching;

namespace NHibernate.Caches.Redis
{
    public class RedisCache : ICache
    {
        private const string CacheNamePrefix = "NHibernate-Cache:";

        private static readonly IInternalLogger log = LoggerProvider.LoggerFor(typeof(RedisCache));

        // The acquired locks do not need to be distributed into Redis because
        // the same ISession will lock/unlock an object.
        private readonly MemoryCache acquiredLocks = new MemoryCache("NHibernate.Caches.Redis.RedisCache");

        private readonly ConnectionMultiplexer connectionMultiplexer;
        private readonly RedisCacheProviderOptions options;
        private readonly TimeSpan expiry;
        private readonly TimeSpan lockTimeout = TimeSpan.FromSeconds(30);

        private const int DefaultExpiry = 300 /*5 minutes*/;

        public string RegionName { get; private set; }
        internal RedisNamespace CacheNamespace { get; private set; }
        public int Timeout { get { return Timestamper.OneMs * 60000; } }

        private class LockData
        {
            public string Key { get; private set; }
            public string LockKey { get; private set; }
            public string LockValue { get; private set; }

            public LockData(string key, string lockKey, string lockValue)
            {
                this.Key = key;
                this.LockKey = lockKey;
                this.LockValue = lockValue;
            }

            public override string ToString()
            {
                return "{ Key='" + Key + "', LockKey='" + LockKey + "', LockValue='" + LockValue + "' }";
            }
        }

        public RedisCache(string regionName, ConnectionMultiplexer connectionMultiplexer, RedisCacheProviderOptions options)
            : this(regionName, new Dictionary<string, string>(), null, connectionMultiplexer, options)
        {

        }

        public RedisCache(string regionName, IDictionary<string, string> properties, RedisCacheElement element, ConnectionMultiplexer connectionMultiplexer, RedisCacheProviderOptions options)
        {
            this.connectionMultiplexer = connectionMultiplexer.ThrowIfNull("connectionMultiplexer");
            this.options = options.ThrowIfNull("options").ShallowCloneAndValidate();

            RegionName = regionName.ThrowIfNull("regionName");

            if (element == null)
            {
                expiry = TimeSpan.FromSeconds(
                    PropertiesHelper.GetInt32(Cfg.Environment.CacheDefaultExpiration, properties, DefaultExpiry)
                );
            }
            else
            {
                expiry = element.Expiration;
            }

            log.DebugFormat("using expiration : {0} seconds", expiry.TotalSeconds);

            var @namespace = CacheNamePrefix + RegionName;

            CacheNamespace = new RedisNamespace(@namespace);
            SyncInitialGeneration();
        }

        public long NextTimestamp()
        {
            return Timestamper.Next();
        }

        private void SyncInitialGeneration()
        {
            try
            {
                if (CacheNamespace.GetGeneration() == -1)
                {
                    var latestGeneration = FetchGeneration();
                    CacheNamespace.SetHigherGeneration(latestGeneration);
                }
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not sync initial generation");

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) throw;
            }
        }

        private long FetchGeneration()
        {
            var db = GetDatabase();

            var generationKey = CacheNamespace.GetGenerationKey();
            var attemptedGeneration = db.StringGet(generationKey);

            if (attemptedGeneration.HasValue)
            {
                log.DebugFormat("using existing generation : {0}", attemptedGeneration);
                return Convert.ToInt64(attemptedGeneration);
            }
            else
            {
                var generation = db.StringIncrement(generationKey);
                log.DebugFormat("creating new generation : {0}", generation);
                return generation;
            }
        }

        public virtual void Put(object key, object value)
        {
            key.ThrowIfNull("key");
            value.ThrowIfNull("value");

            log.DebugFormat("put in cache : {0}", key);

            try
            {
                var data = Serialize(value);

                ExecuteEnsureGeneration(transaction =>
                {
                    var cacheKey = CacheNamespace.GetKey(key);
                    transaction.StringSetAsync(cacheKey, data, expiry);

                    var setOfKeysKey = CacheNamespace.GetSetOfKeysKey();
                    transaction.SetAddAsync(setOfKeysKey, cacheKey);
                });
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not put in cache : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) throw;
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
                    var cacheKey = CacheNamespace.GetKey(key);
                    dataResult = transaction.StringGetAsync(cacheKey);
                });

                var data = dataResult.Result;

                return Deserialize(data);
            }
            catch (Exception e)
            {
                log.ErrorFormat("coult not get from cache : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) throw;

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
                    var cacheKey = CacheNamespace.GetKey(key);
                    transaction.KeyDeleteAsync(cacheKey, CommandFlags.FireAndForget);
                });
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not remove from cache : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) throw;
            }
        }

        public virtual void Clear()
        {
            var generationKey = CacheNamespace.GetGenerationKey();
            var setOfKeysKey = CacheNamespace.GetSetOfKeysKey();

            log.DebugFormat("clear cache : {0}", generationKey);

            try
            {
                var db = GetDatabase();
                var transaction = db.CreateTransaction();

                var generationIncrement = transaction.StringIncrementAsync(generationKey);

                transaction.KeyDeleteAsync(setOfKeysKey, CommandFlags.FireAndForget);

                transaction.Execute();

                CacheNamespace.SetHigherGeneration(generationIncrement.Result);
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not clear cache : {0}", generationKey);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) throw;
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
                var lockKey = CacheNamespace.GetLockKey(key);

                Retry.UntilTrue(() =>
                {
                    var lockData = new LockData(
                        key: Convert.ToString(key),
                        lockKey: lockKey,
                        lockValue: GetLockValue()
                    );

                    var db = GetDatabase();
                    var wasLockTaken = db.LockTake(lockData.LockKey, lockData.LockValue, lockTimeout);

                    if (wasLockTaken)
                    {
                        // It's ok to use Set() instead of Add() because we know
                        // that only one thread will access the MemoryCache (per-ISession).
                        acquiredLocks.Set(lockData.Key, lockData, absoluteExpiration: DateTime.UtcNow.Add(lockTimeout));
                    }

                    return wasLockTaken;
                }, lockTimeout);
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not acquire cache lock : {0}", key);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) throw;
            }
        }

        public virtual void Unlock(object key)
        {
            // Use Remove() instead of Get() because we are releasing the lock
            // anyways.
            var lockData = acquiredLocks.Remove(Convert.ToString(key)) as LockData;
            if (lockData == null)
            {
                log.WarnFormat("attempted to unlock '{0}' but a previous lock was not acquired", key);
                return;
            }

            log.DebugFormat("releasing cache lock : {0}", lockData);

            try
            {
                var db = GetDatabase();

                var wasLockReleased = db.LockRelease(lockData.LockKey, lockData.LockValue);

                if (!wasLockReleased)
                {
                    log.WarnFormat("attempted to unlock '{0}' but it could not be relased (maybe was cleared from Redis)", lockData); 
                }
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not release cache lock : {0}", lockData);

                var evtArg = new RedisCacheExceptionEventArgs(e);
                OnException(evtArg);
                if (evtArg.Throw) throw;
            }
        }

        private void ExecuteEnsureGeneration(Action<StackExchange.Redis.ITransaction> action)
        {
            var maxRetries = 5;
            var attempt = 1;

            while (attempt <= maxRetries)
            {
                var db = GetDatabase();
                var transaction = db.CreateTransaction();

                // We optimistically execute the transaction with the existing
                // generation. When the generation has changed, the transaction
                // fails and we sync the generation and retry the transaction.
                transaction.AddCondition(Condition.StringEqual(CacheNamespace.GetGenerationKey(), CacheNamespace.GetGeneration()));

                action(transaction);

                var executed = transaction.Execute();

                if (executed)
                {
                    return;
                }
                else
                {
                    SyncGeneration(db);
                }
            }

            var message = String.Format(
                "Could not execute transaction with synchronized generation (failed after {0} attempts).",
                maxRetries
            );
            throw new RedisCacheGenerationException(message);
        }

        private void SyncGeneration(IDatabase db)
        {
            var generationKey = CacheNamespace.GetGenerationKey();
            var generation = db.StringGet(generationKey);
            var serverGeneration = Convert.ToInt64(generation);
            CacheNamespace.SetHigherGeneration(serverGeneration);

            // The generation on the server may have been removed.
            if (serverGeneration < CacheNamespace.GetGeneration())
            {
                db.StringSetAsync(
                    key: CacheNamespace.GetGenerationKey(),
                    value: CacheNamespace.GetGeneration(),
                    flags: CommandFlags.FireAndForget
                );
            }
        }

        private string GetLockValue()
        {
            return options.LockValueFactory();
        }

        private RedisValue Serialize(object value)
        {
            return options.Serializer.Serialize(value);
        }

        private object Deserialize(RedisValue value)
        {
            return options.Serializer.Deserialize(value);
        }

        private IDatabase GetDatabase()
        {
            return connectionMultiplexer.GetDatabase(options.Database);
        }

        private void OnException(RedisCacheExceptionEventArgs e)
        {
            options.OnException(e);
        }
    }
}