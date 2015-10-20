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
        // TODO: IGeneration
        // TODO: Region cache namespaces

        /// <summary>
        /// Get or set the serializer used for serializing/deserializing
        /// values from Redis.
        /// </summary>
        public ICacheSerializer Serializer { get; set; }

        /// <summary>
        /// Get or set a handler for when exceptions occur during cache
        /// operations. This must be thread-safe.
        /// </summary>
        public Action<RedisCacheExceptionEventArgs> OnException { get; set; }

        /// <summary>
        /// Get or set the strategy used when determining whether or not to retry
        /// a lock take.
        /// </summary>
        public ILockTakeRetryStrategy LockTakeRetryStrategy { get; set; }

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
        /// For example, this is helpful if you want to identify where the 
        /// lock was created from (such as including the machine name, process
        /// id and a random Guid). This must be thread-safe.
        /// </summary>
        public Func<string> LockValueFactory { get; set; }

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
            LockTakeRetryStrategy = new ExponentialBackoffWithJitterLockTakeRetryStrategy();
            OnLockFailed = DefaultOnLockFailed;
            OnUnlockFailed = DefaultOnUnlockFailed;
            LockValueFactory = DefaultLockValueFactory;
            Database = 0;
            CacheConfigurations = Enumerable.Empty<RedisCacheConfiguration>();
        }

        // Copy constructor.
        private RedisCacheProviderOptions(RedisCacheProviderOptions options)
        {
            Serializer = options.Serializer;
            OnException = options.OnException;
            LockTakeRetryStrategy = options.LockTakeRetryStrategy;
            OnLockFailed = options.OnLockFailed;
            OnUnlockFailed = options.OnUnlockFailed;
            LockValueFactory = options.LockValueFactory;
            Database = options.Database;
            CacheConfigurations = options.CacheConfigurations;
        }
        
        private static string DefaultLockValueFactory()
        {
            return "lock-" + Guid.NewGuid();
        }

        private static void DefaultOnException(RedisCacheExceptionEventArgs e)
        {
            e.Throw = true;
        }

        private static void DefaultOnUnlockFailed(UnlockFailedEventArgs e)
        {

        }

        private static void DefaultOnLockFailed(LockFailedEventArgs e)
        {
            throw new TimeoutException(
                String.Format("Lock take for '{0}' exceeded timeout {1}.", e.LockKey, e.LockTakeTimeout)
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

            if (clone.LockTakeRetryStrategy == null)
            {
                throw new InvalidOperationException("A lock take retry strategy was not configured on the " + name + ".");
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
