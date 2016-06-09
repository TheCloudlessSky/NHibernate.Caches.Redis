using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheProviderOptions
    {
        /// <summary>
        /// Get or set the serializer used for serializing/deserializing
        /// values from Redis.
        /// </summary>
        public ICacheSerializer Serializer { get; set; }

        /// <summary>
        /// An event raised when exceptions occur during cache operations.
        /// If an event handler is not added, by default exceptions are
        /// thrown.
        /// This must be thread-safe.
        /// </summary>
        public event RedisCacheEventHandler<ExceptionEventArgs> Exception;

        /// <summary>
        /// Get or set the strategy used when determining whether or not to retry
        /// acquiring a lock.
        /// </summary>
        public IAcquireLockRetryStrategy AcquireLockRetryStrategy { get; set; }

        /// <summary>
        /// An event raised when locking fails (for any reason other than an
        /// exception). If an event handler is not added, by default a 
        /// <see cref="TimeoutException"/> is thrown. This must be thread-safe.
        /// </summary>
        public event RedisCacheEventHandler<LockFailedEventArgs> LockFailed;

        /// <summary>
        /// An event raised when unlocking fails (for any reason other
        /// than an exception). This must be thread-safe.
        /// </summary>
        public event RedisCacheEventHandler<UnlockFailedEventArgs> UnlockFailed;

        /// <summary>
        /// Get or set a factory used for creating the value of the locks.
        /// </summary>
        public ILockValueFactory LockValueFactory { get; set; }

        /// <summary>
        /// Control which Redis database is used for the cache.
        /// </summary>
        public int Database { get; set; }

        /// <summary>
        /// Get or set the configuration for each region's cache.
        /// </summary>
        public IEnumerable<RedisCacheConfiguration> CacheConfigurations { get; set; }

        /// <summary>
        /// Defines the prefix used for all cache keys. The default value is "NHibernate-Cache:".
        /// If Redis is only used for NHibernate caching, you can set this to an empty
        /// string to reduce Redis memory.
        /// </summary>
        public string KeyPrefix { get; set; }

	    public RedisCacheProviderOptions()
        {
            Serializer = new NetDataContractCacheSerializer();
            AcquireLockRetryStrategy = new ExponentialBackoffWithJitterAcquireLockRetryStrategy();
            LockValueFactory = new GuidLockValueFactory();
            Database = 0;
            CacheConfigurations = Enumerable.Empty<RedisCacheConfiguration>();
            KeyPrefix = "NHibernate-Cache:";
        }

        // Copy constructor.
        private RedisCacheProviderOptions(RedisCacheProviderOptions options)
        {
            Serializer = options.Serializer;
            Exception = options.Exception;
            AcquireLockRetryStrategy = options.AcquireLockRetryStrategy;
            LockFailed = options.LockFailed;
            UnlockFailed = options.UnlockFailed;
            LockValueFactory = options.LockValueFactory;
            Database = options.Database;
            CacheConfigurations = options.CacheConfigurations;
        }

        internal RedisCacheProviderOptions ShallowCloneAndValidate()
        {
            var clone = new RedisCacheProviderOptions(this);

            var name = typeof(RedisCacheProviderOptions).Name;

            if (clone.Serializer == null)
            {
                throw new InvalidOperationException("A serializer was not configured on the " + name + ".");
            }

            if (clone.AcquireLockRetryStrategy == null)
            {
                throw new InvalidOperationException("An acquire lock retry strategy was not configured on the " + name + ".");
            }

            if (clone.LockValueFactory == null)
            {
                throw new InvalidOperationException("A lock value factory was not confugred on the " + name + ".");
            }

            if (clone.CacheConfigurations == null)
            {
                throw new InvalidOperationException("The cache configurations cannot be null on the " + name + ".");
            }

            return clone;
        }

        internal void OnException(RedisCache sender, ExceptionEventArgs e)
        {
            var onException = Exception;

            if (onException == null)
            {
                e.Throw = true;
            }
            else
            {
                onException(sender, e);
            }
        }

        internal void OnUnlockFailed(RedisCache sender, UnlockFailedEventArgs e)
        {
            var onUnlockFailed = UnlockFailed;

            if (onUnlockFailed == null)
            {
                // No-op.
            }
            else
            {
                onUnlockFailed(sender, e);
            }
        }

        internal void OnLockFailed(RedisCache sender, LockFailedEventArgs e)
        {
            var onLockFailed = LockFailed;

            if (onLockFailed == null)
            {
                throw new TimeoutException(
                    String.Format("Acquiring lock for '{0}' exceeded timeout '{1}'.", e.Key, e.AcquireLockTimeout)
                );
            }
            else
            {
                onLockFailed(sender, e);
            }
        }
    }
}
