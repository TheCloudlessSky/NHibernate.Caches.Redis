using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
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

    public static class DateTimeExtensions
    {
        public const long UnixEpoch = 621355968000000000L;
        private static readonly DateTime MinDateTimeUtc = new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToUnixTime(this DateTime dateTime)
        {
            return (dateTime.ToStableUniversalTime().Ticks - UnixEpoch) / TimeSpan.TicksPerSecond;
        }

        public static DateTime ToStableUniversalTime(this DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
                return dateTime;
            if (dateTime == DateTime.MinValue)
                return MinDateTimeUtc;

#if SILVERLIGHT
			// Silverlight 3, 4 and 5 all work ok with DateTime.ToUniversalTime, but have no TimeZoneInfo.ConverTimeToUtc implementation.
			return dateTime.ToUniversalTime();
#else
            // .Net 2.0 - 3.5 has an issue with DateTime.ToUniversalTime, but works ok with TimeZoneInfo.ConvertTimeToUtc.
            // .Net 4.0+ does this under the hood anyway.
            return TimeZoneInfo.ConvertTimeToUtc(dateTime);
#endif
        }
    }

    public static class ExecExtensions
    {
        public static void RetryUntilTrue(Func<bool> action, TimeSpan? timeOut)
        {
            var i = 0;
            var firstAttempt = DateTime.UtcNow;

            while (timeOut == null || DateTime.UtcNow - firstAttempt < timeOut.Value)
            {
                i++;
                if (action())
                {
                    return;
                }
                SleepBackOffMultiplier(i);
            }

            throw new TimeoutException(string.Format("Exceeded timeout of {0}", timeOut.Value));
        }

        private static void SleepBackOffMultiplier(int i)
        {
            var rand = new Random(Guid.NewGuid().GetHashCode());
            var nextTry = rand.Next(
                (int)Math.Pow(i, 2), (int)Math.Pow(i + 1, 2) + 1);

            Thread.Sleep(nextTry);
        }
    }

    public interface ISerializer
    {
        byte[] Serialize(object value);

        object Deserialize(byte[] someBytes);
    }

    public class ObjectSerializer : ISerializer
    {
        protected readonly BinaryFormatter Bf = new BinaryFormatter();

        public virtual byte[] Serialize(object value)
        {
            if (value == null)
                return null;
            var memoryStream = new MemoryStream();
            memoryStream.Seek(0, 0);
            Bf.Serialize(memoryStream, value);
            return memoryStream.ToArray();
        }

        public virtual object Deserialize(byte[] someBytes)
        {
            if (someBytes == null)
                return null;
            var memoryStream = new MemoryStream();
            memoryStream.Write(someBytes, 0, someBytes.Length);
            memoryStream.Seek(0, 0);
            var de = Bf.Deserialize(memoryStream);
            return de;
        }
    }

    public class RedisNamespace
    {

        private const string UniqueCharacter = "?";

        //make reserved keys unique by tacking N of these to the beginning of the string
        private const string ReservedTag = "@" + UniqueCharacter + "@";

        //unique separator between namespace and key
        private const string NamespaceKeySeparator = "#" + UniqueCharacter + "#";

        //make non-static keys unique by tacking on N of these to the end of the string
        public const string KeyTag = "%" + UniqueCharacter + "%";

        public const string NamespaceTag = "!" + UniqueCharacter + "!";

        //remove any odd numbered runs of the UniqueCharacter character
        private const string Sanitizer = UniqueCharacter + UniqueCharacter;

        // namespace generation - when generation changes, namespace is slated for garbage collection
        private long _namespaceGeneration = -1;

        // key for namespace generation
        private readonly string _namespaceGenerationKey;

        //sanitized name for namespace (includes namespace generation)
        private readonly string _namespacePrefix;

        //reserved, unique name for meta entries for this namespace

        // key for set of all global keys in this namespace
        private readonly string _globalKeysKey;

        // key for list of keys slated for garbage collection
        public const string NamespacesGarbageKey = ReservedTag + "REDIS_NAMESPACES_GARBAGE";

        public const int NumTagsForKey = 0;
        public const int NumTagsForLockKey = 1;

        public RedisNamespace(string name)
        {
            _namespacePrefix = Sanitize(name);

            var namespaceReservedName = NamespaceTag + _namespacePrefix;

            _globalKeysKey = namespaceReservedName;

            //get generation
            _namespaceGenerationKey = namespaceReservedName + "_" + "generation";

            LockingStrategy = new ReaderWriterLockingStrategy();
        }
        /// <summary>
        /// get locking strategy
        /// </summary>
        public ILockingStrategy LockingStrategy
        {
            get;
            set;
        }
        /// <summary>
        /// get current generation
        /// </summary>
        /// <returns></returns>
        public long GetGeneration()
        {
            using (LockingStrategy.ReadLock())
            {
                return _namespaceGeneration;
            }
        }
        /// <summary>
        /// set new generation
        /// </summary>
        /// <param name="generation"></param>
        public void SetGeneration(long generation)
        {
            if (generation < 0) return;

            using (LockingStrategy.WriteLock())
            {
                if (_namespaceGeneration == -1 || generation > _namespaceGeneration)
                    _namespaceGeneration = generation;
            }
        }
        /// <summary>
        /// redis key for generation
        /// </summary>
        /// <returns></returns>
        public string GetGenerationKey()
        {
            return _namespaceGenerationKey;
        }

        /// <summary>
        /// get redis key that holds all namespace keys
        /// </summary>
        /// <returns></returns>
        public string GetGlobalKeysKey()
        {
            return _globalKeysKey;
        }
        /// <summary>
        /// get global cache key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GlobalCacheKey(object key)
        {
            return GlobalKey(key, NumTagsForKey);
        }

        public string GlobalLockKey(object key)
        {
            return GlobalKey(key, NumTagsForLockKey) + "LOCK";
        }

        /// <summary>
        /// get global key inside of this namespace
        /// </summary>
        /// <param name="key"></param>
        /// <param name="numUniquePrefixes">prefixes can be added for name deconfliction</param>
        /// <returns></returns>
        public string GlobalKey(object key, int numUniquePrefixes)
        {
            var rc = Sanitize(key);
            if (_namespacePrefix != null && !_namespacePrefix.Equals(""))
                rc = _namespacePrefix + "_" + GetGeneration() + NamespaceKeySeparator + rc;
            for (var i = 0; i < numUniquePrefixes; ++i)
                rc += KeyTag;
            return rc;
        }
        /// <summary>
        /// replace UniqueCharacter with its double, to avoid name clash
        /// </summary>
        /// <param name="dirtyString"></param>
        /// <returns></returns>
        private static string Sanitize(string dirtyString)
        {
            return dirtyString == null ? null : dirtyString.Replace(UniqueCharacter, Sanitizer);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dirtyString"></param>
        /// <returns></returns>
        private static string Sanitize(object dirtyString)
        {
            return Sanitize(dirtyString.ToString());
        }
    }

    public interface ILockingStrategy
    {
        IDisposable ReadLock();

        IDisposable WriteLock();
    }

    public class ReaderWriterLockingStrategy : ILockingStrategy
    {
        private readonly ReaderWriterLockSlim _lockObject = new ReaderWriterLockSlim();


        public IDisposable ReadLock()
        {
            return new ReadLock(_lockObject);
        }

        public IDisposable WriteLock()
        {
            return new WriteLock(_lockObject);
        }
    }

    /// <summary>
    /// This class manages a read lock for a local readers/writer lock, 
    /// using the Resource Acquisition Is Initialization pattern
    /// </summary>
    public class ReadLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _lockObject;

        /// <summary>
        /// RAII initialization 
        /// </summary>
        /// <param name="lockObject"></param>
        public ReadLock(ReaderWriterLockSlim lockObject)
        {
            _lockObject = lockObject;
            lockObject.EnterReadLock();
        }

        /// <summary>
        /// RAII disposal
        /// </summary>
        public void Dispose()
        {
            _lockObject.ExitReadLock();
        }
    }

    public class WriteLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _lockObject;

        /// <summary>
        /// This class manages a write lock for a local readers/writer lock, 
        /// using the Resource Acquisition Is Initialization pattern
        /// </summary>
        /// <param name="lockObject"></param>
        public WriteLock(ReaderWriterLockSlim lockObject)
        {
            _lockObject = lockObject;
            lockObject.EnterWriteLock();
        }

        /// <summary>
        /// RAII disposal
        /// </summary>
        public void Dispose()
        {
            _lockObject.ExitWriteLock();
        }
    }
}