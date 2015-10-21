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
        /// Get or set a handler for when exceptions occur during cache
        /// operations. This must be thread-safe.
        /// </summary>
        public Action<ExceptionEventArgs> OnException { get; set; }

        /// <summary>
        /// Get or set the strategy used when determining whether or not to retry
        /// acquiring a lock.
        /// </summary>
        public IAcquireLockRetryStrategy AcquireLockRetryStrategy { get; set; }

        /// <summary>
        /// Get or set a handler for when locking fails (for any reason other
        /// than an exception). By default a <see cref="TimeoutException"/> is thrown.
        /// This must be thread-safe.
        /// </summary>
        public Action<LockFailedEventArgs> OnLockFailed { get; set; }

        /// <summary>
        /// Get or set a handler for when unlocking fails (for any reason other
        /// than an exception). This must be thread-safe.
        /// </summary>
        public Action<UnlockFailedEventArgs> OnUnlockFailed { get; set; }

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

        public RedisCacheProviderOptions()
        {
            Serializer = new NetDataContractCacheSerializer();
            OnException = DefaultOnException;
            AcquireLockRetryStrategy = new ExponentialBackoffWithJitterAcquireLockRetryStrategy();
            OnLockFailed = DefaultOnLockFailed;
            OnUnlockFailed = DefaultOnUnlockFailed;
            LockValueFactory = new GuidLockValueFactory();
            Database = 0;
            CacheConfigurations = Enumerable.Empty<RedisCacheConfiguration>();
        }

        // Copy constructor.
        private RedisCacheProviderOptions(RedisCacheProviderOptions options)
        {
            Serializer = options.Serializer;
            OnException = options.OnException;
            AcquireLockRetryStrategy = options.AcquireLockRetryStrategy;
            OnLockFailed = options.OnLockFailed;
            OnUnlockFailed = options.OnUnlockFailed;
            LockValueFactory = options.LockValueFactory;
            Database = options.Database;
            CacheConfigurations = options.CacheConfigurations;
        }

        private static void DefaultOnException(ExceptionEventArgs e)
        {
            e.Throw = true;
        }

        private static void DefaultOnUnlockFailed(UnlockFailedEventArgs e)
        {

        }

        private static void DefaultOnLockFailed(LockFailedEventArgs e)
        {
            throw new TimeoutException(
                String.Format("Acquiring lock for '{0}' exceeded timeout '{1}'.", e.Key, e.AcquireLockTimeout)
            );
        }

        internal RedisCacheProviderOptions ShallowCloneAndValidate()
        {
            var clone = new RedisCacheProviderOptions(this);

            var name = typeof(RedisCacheProviderOptions).Name;

            if (clone.Serializer == null)
            {
                throw new InvalidOperationException("A serializer was not configured on the " + name + ".");
            }

            if (clone.OnException == null)
            {
                throw new InvalidOperationException("A handler for on exception was not confugred on the " + name + ".");                
            }

            if (clone.AcquireLockRetryStrategy == null)
            {
                throw new InvalidOperationException("An acquire lock retry strategy was not configured on the " + name + ".");
            }

            if (clone.OnLockFailed == null)
            {
                throw new InvalidOperationException("A handler for on lock failed was not configured on the " + name + ".");
            }

            if (clone.OnUnlockFailed == null)
            {
                throw new InvalidOperationException("A handler for on unlock failed was not configured on the " + name + ".");
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
    }
}
