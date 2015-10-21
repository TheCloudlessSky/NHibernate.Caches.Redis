using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate.Util;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheConfiguration
    {
        public static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan DefaultAcquireLockTimeout = DefaultLockTimeout;

        public string RegionName { get; private set; }

        /// <summary>
        /// Gets the duration that the item remains in the cache.
        /// </summary>
        public TimeSpan Expiration { get; private set; }

        /// <summary>
        /// Gets the maximum duration that the item can be locked.
        /// </summary>
        public TimeSpan LockTimeout { get; private set; }

        /// <summary>
        /// Gets the maximum duration to wait when acquiring a lock for the 
        /// item. By default, this is the same as the lock timeout.
        /// </summary>
        public TimeSpan AcquireLockTimeout { get; private set; }

        public RedisCacheConfiguration(string regionName)
            : this(regionName, DefaultExpiration, DefaultLockTimeout)
        {

        }

        public RedisCacheConfiguration(string regionName, TimeSpan? expiration = null, TimeSpan? lockTimeout = null, TimeSpan? acquireLockTimeout = null)
        {
            this.RegionName = regionName.ThrowIfNull("regionName");
            this.Expiration = expiration ?? DefaultExpiration;
            this.LockTimeout = lockTimeout ?? DefaultLockTimeout;
            this.AcquireLockTimeout = acquireLockTimeout ?? DefaultAcquireLockTimeout;
        }

        internal static RedisCacheConfiguration FromPropertiesOrDefaults(string regionName, IDictionary<string, string> properties)
        {
            var expiration = TimeSpan.FromSeconds(
                PropertiesHelper.GetInt32(Cfg.Environment.CacheDefaultExpiration, properties, (int)DefaultExpiration.TotalSeconds)
            );
            return new RedisCacheConfiguration(
                regionName: regionName,
                expiration: expiration,
                lockTimeout: DefaultLockTimeout,
                acquireLockTimeout: DefaultAcquireLockTimeout
            );
        }
    }
}
