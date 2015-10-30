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
        private const string cacheNamespacePrefix = "NHibernate-Cache:";

        private static readonly IInternalLogger log = LoggerProvider.LoggerFor(typeof(RedisCache));

        // The acquired locks do not need to be distributed into Redis because
        // the same ISession will lock/unlock an object.
        private readonly MemoryCache acquiredLocks = new MemoryCache("NHibernate.Caches.Redis.RedisCache");

        private static readonly LuaScript getScript = LuaScript.Prepare(@"
if redis.call('sismember', @setOfActiveKeysKey, @key) == 1 then
    local result = redis.call('get', @key)
    if result == nil then
        redis.call('srem', @setOfActiveKeysKey, @key)
    end
    return result
else
    redis.call('del', @key)
    return nil
end
");
        private static readonly LuaScript slidingExpirationScript = LuaScript.Prepare(@"
local pttl = redis.call('pttl', @key)
if pttl <= tonumber(@slidingExpiration) then
    redis.call('pexpire', @key, @expiration)
    return true
else
    return false
end
");

        private static readonly LuaScript putScript = LuaScript.Prepare(@"
redis.call('sadd', @setOfActiveKeysKey, @key)
redis.call('set', @key, @value, 'PX', @expiration)
");
        private static readonly LuaScript removeScript = LuaScript.Prepare(@"
redis.call('srem', @setOfActiveKeysKey, @key)
redis.call('del', @key)
");
        private LuaScript unlockScript = LuaScript.Prepare(@"
if redis.call('get', @lockKey) == @lockValue then
    return redis.call('del', @lockKey)
else
    return 0
end
");

// Help with debugging scripts since exceptions are swallowed with FireAndForget.
#if DEBUG
        private const CommandFlags fireAndForgetFlags = CommandFlags.None;
#else
        private const CommandFlags fireAndForgetFlags = CommandFlags.FireAndForget;
#endif

        private readonly ConnectionMultiplexer connectionMultiplexer;
        private readonly RedisCacheProviderOptions options;
        private readonly TimeSpan expiration;
        private readonly TimeSpan slidingExpiration;
        private readonly TimeSpan lockTimeout;
        private readonly TimeSpan acquireLockTimeout;

        public string RegionName { get; private set; }
        internal RedisNamespace CacheNamespace { get; private set; }

        public int Timeout { get { return Timestamper.OneMs * (int)lockTimeout.TotalMilliseconds; } }

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
            : this(new RedisCacheConfiguration(regionName), connectionMultiplexer, options)
        {

        }

        public RedisCache(RedisCacheConfiguration configuration, ConnectionMultiplexer connectionMultiplexer, RedisCacheProviderOptions options)
        {
            configuration.ThrowIfNull("configuration")
                .Validate();
            RegionName = configuration.RegionName;
            expiration = configuration.Expiration;
            slidingExpiration = configuration.SlidingExpiration;
            lockTimeout = configuration.LockTimeout;
            acquireLockTimeout = configuration.AcquireLockTimeout;

            this.connectionMultiplexer = connectionMultiplexer.ThrowIfNull("connectionMultiplexer");
            this.options = options.ThrowIfNull("options")
                .ShallowCloneAndValidate();

            log.DebugFormat("creating cache: regionName='{0}', expiration='{1}', lockTimeout='{2}', acquireLockTimeout='{3}'", 
                RegionName, expiration, lockTimeout, acquireLockTimeout
            );

            CacheNamespace = new RedisNamespace(cacheNamespacePrefix + RegionName);
        }

        public long NextTimestamp()
        {
            return Timestamper.Next();
        }

        public virtual void Put(object key, object value)
        {
            key.ThrowIfNull("key");
            value.ThrowIfNull("value");

            log.DebugFormat("put in cache: regionName='{0}', key='{1}'", RegionName, key);

            try
            {
                var serializedValue = options.Serializer.Serialize(value);

                var cacheKey = CacheNamespace.GetKey(key);
                var setOfActiveKeysKey = CacheNamespace.GetSetOfActiveKeysKey();
                var db = GetDatabase();

                db.ScriptEvaluate(putScript, new
                {
                    key = cacheKey,
                    setOfActiveKeysKey = setOfActiveKeysKey,
                    value = serializedValue,
                    expiration = expiration.TotalMilliseconds
                }, fireAndForgetFlags);
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not put in cache: regionName='{0}', key='{1}'", RegionName, key);

                var evtArg = new ExceptionEventArgs(RegionName, RedisCacheMethod.Put, e);
                options.OnException(this, evtArg);
                if (evtArg.Throw)
                {
                    throw new RedisCacheException(RegionName, "Failed to put item in cache. See inner exception.", e);
                }
            }
        }

        public virtual object Get(object key)
        {
            key.ThrowIfNull();

            log.DebugFormat("get from cache: regionName='{0}', key='{1}'", RegionName, key);

            try
            {
                var cacheKey = CacheNamespace.GetKey(key);
                var setOfActiveKeysKey = CacheNamespace.GetSetOfActiveKeysKey();

                var db = GetDatabase();

                var resultValues = (RedisValue[])db.ScriptEvaluate(getScript, new
                {
                    key = cacheKey,
                    setOfActiveKeysKey = setOfActiveKeysKey
                });

                if (resultValues[0].IsNullOrEmpty)
                {
                    log.DebugFormat("cache miss: regionName='{0}', key='{1}'", RegionName, key);
                    return null;
                }
                else
                {
                    var serializedResult = resultValues[0];

                    var deserializedValue = options.Serializer.Deserialize(serializedResult);

                    if (deserializedValue != null && slidingExpiration != RedisCacheConfiguration.NoSlidingExpiration)
                    {
                        db.ScriptEvaluate(slidingExpirationScript, new
                        {
                            key = cacheKey,
                            expiration = expiration.TotalMilliseconds,
                            slidingExpiration = slidingExpiration.TotalMilliseconds
                        }, fireAndForgetFlags);
                    }

                    return deserializedValue;
                }
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not get from cache: regionName='{0}', key='{1}'", RegionName, key);

                var evtArg = new ExceptionEventArgs(RegionName, RedisCacheMethod.Get, e);
                options.OnException(this, evtArg);
                if (evtArg.Throw)
                {
                    throw new RedisCacheException(RegionName, "Failed to get item from cache. See inner exception.", e);
                }

                return null;
            }
        }

        public virtual void Remove(object key)
        {
            key.ThrowIfNull();

            log.DebugFormat("remove from cache: regionName='{0}', key='{1}'", RegionName, key);

            try
            {
                var cacheKey = CacheNamespace.GetKey(key);
                var setOfActiveKeysKey = CacheNamespace.GetSetOfActiveKeysKey();
                var db = GetDatabase();

                db.ScriptEvaluate(removeScript, new
                {
                    key = cacheKey,
                    setOfActiveKeysKey = setOfActiveKeysKey
                }, fireAndForgetFlags);
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not remove from cache: regionName='{0}', key='{1}'", RegionName, key);

                var evtArg = new ExceptionEventArgs(RegionName, RedisCacheMethod.Remove, e);
                options.OnException(this, evtArg);
                if (evtArg.Throw)
                {
                    throw new RedisCacheException(RegionName, "Failed to remove item from cache. See inner exception.", e);
                }
            }
        }

        public virtual void Clear()
        {
            log.DebugFormat("clear cache: regionName='{0}'", RegionName);

            try
            {
                var setOfActiveKeysKey = CacheNamespace.GetSetOfActiveKeysKey();
                var db = GetDatabase();
                db.KeyDelete(setOfActiveKeysKey, fireAndForgetFlags);
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not clear cache: regionName='{0}'", RegionName);

                var evtArg = new ExceptionEventArgs(RegionName, RedisCacheMethod.Clear, e);
                options.OnException(this, evtArg);
                if (evtArg.Throw)
                {
                    throw new RedisCacheException(RegionName, "Failed to clear cache. See inner exception.", e);
                }
            }
        }

        public virtual void Destroy()
        {
            // No-op since Redis is distributed.
            log.DebugFormat("destroying cache: regionName='{0}'", RegionName);
        }

        public virtual void Lock(object key)
        {
            log.DebugFormat("acquiring cache lock: regionName='{0}', key='{1}'", RegionName, key);

            try
            {
                var lockKey = CacheNamespace.GetLockKey(key);
                var shouldRetry = options.AcquireLockRetryStrategy.GetShouldRetry();

                var wasLockAcquired = false;
                var shouldTryAcquireLock = true;
                
                while (shouldTryAcquireLock)
                {
                    var lockData = new LockData(
                        key: Convert.ToString(key),
                        lockKey: lockKey,
                        // Recalculated each attempt to ensure a unique value.
                        lockValue: options.LockValueFactory.GetLockValue()
                    );

                    if (TryAcquireLock(lockData))
                    {
                        wasLockAcquired = true;
                        shouldTryAcquireLock = false;
                    }
                    else
                    {
                        var shouldRetryArgs = new ShouldRetryAcquireLockArgs(
                            RegionName, lockData.Key, lockData.LockKey,
                            lockData.LockValue, lockTimeout, acquireLockTimeout
                        );
                        shouldTryAcquireLock = shouldRetry(shouldRetryArgs);
                    }
                }

                if (!wasLockAcquired)
                {
                    var lockFailedArgs = new LockFailedEventArgs(
                        RegionName, key, lockKey,
                        lockTimeout, acquireLockTimeout
                    );
                    options.OnLockFailed(this, lockFailedArgs);
                }
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not acquire cache lock: regionName='{0}', key='{1}'", RegionName, key);

                var evtArg = new ExceptionEventArgs(RegionName, RedisCacheMethod.Lock, e);
                options.OnException(this, evtArg);
                if (evtArg.Throw)
                {
                    throw new RedisCacheException(RegionName, "Failed to lock item in cache. See inner exception.", e);
                }
            }
        }

        private bool TryAcquireLock(LockData lockData)
        {
            var db = GetDatabase();

            // Don't use IDatabase.LockTake() because we don't use the matching
            // LockRelease(). So, avoid any confusion. Besides, LockTake() just
            // calls this anyways.
            var wasLockAcquired = db.StringSet(lockData.LockKey, lockData.LockValue, lockTimeout, When.NotExists);

            if (wasLockAcquired)
            {
                // It's ok to use Set() instead of Add() because the lock in 
                // Redis will cause other clients to wait.
                acquiredLocks.Set(lockData.Key, lockData, absoluteExpiration: DateTime.UtcNow.Add(lockTimeout));
            }

            return wasLockAcquired;
        }

        public virtual void Unlock(object key)
        {
            // Use Remove() instead of Get() because we are releasing the lock
            // anyways.
            var lockData = acquiredLocks.Remove(Convert.ToString(key)) as LockData;
            if (lockData == null)
            {
                log.WarnFormat("attempted to unlock '{0}' but a previous lock was not acquired or timed out", key);
                var unlockFailedEventArgs = new UnlockFailedEventArgs(
                    RegionName, key, lockKey: null, lockValue: null
                );
                options.OnUnlockFailed(this, unlockFailedEventArgs);
                return;
            }

            log.DebugFormat("releasing cache lock: regionName='{0}', key='{1}', lockKey='{2}', lockValue='{3}'", 
                RegionName, lockData.Key, lockData.LockKey, lockData.LockValue
            );

            try
            {
                var db = GetDatabase();

                // Don't use IDatabase.LockRelease() because it uses watch/unwatch
                // where we prefer an atomic operation (via a script).
                var wasLockReleased = (bool)db.ScriptEvaluate(unlockScript, new
                {
                    lockKey = lockData.LockKey,
                    lockValue = lockData.LockValue
                });

                if (!wasLockReleased)
                {
                    log.WarnFormat("attempted to unlock '{0}' but it could not be relased (maybe timed out or was cleared in Redis)", lockData);

                    var unlockFailedEventArgs = new UnlockFailedEventArgs(
                        RegionName, key, lockData.LockKey, lockData.LockValue
                    );
                    options.OnUnlockFailed(this, unlockFailedEventArgs);
                }
            }
            catch (Exception e)
            {
                log.ErrorFormat("could not release cache lock: regionName='{0}', key='{1}', lockKey='{2}', lockValue='{3}'",
                    RegionName, lockData.Key, lockData.LockKey, lockData.LockValue
                );

                var evtArg = new ExceptionEventArgs(RegionName, RedisCacheMethod.Unlock, e);
                options.OnException(this, evtArg);
                if (evtArg.Throw)
                {
                    throw new RedisCacheException(RegionName, "Failed to unlock item in cache. See inner exception.", e);
                }
            }
        }

        private IDatabase GetDatabase()
        {
            return connectionMultiplexer.GetDatabase(options.Database);
        }
    }
}