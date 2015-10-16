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
        /// operations.
        /// </summary>
        public Action<RedisCacheExceptionEventArgs> OnException { get; set; }
        
        /// <summary>
        /// Get or set a factory used for creating the value of the locks.
        /// For example, this is helpful if you want to identify where the 
        /// lock was created from (such as including the machine name, process
        /// id and a random Guid).
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
            LockValueFactory = DefaultLockValueFactory;
            Database = 0;
            CacheConfigurations = Enumerable.Empty<RedisCacheConfiguration>();
        }

        // Copy constructor.
        private RedisCacheProviderOptions(RedisCacheProviderOptions options)
        {
            Serializer = options.Serializer;
            OnException = options.OnException;
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
