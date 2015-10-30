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
        public static readonly TimeSpan NoSlidingExpiration = TimeSpan.Zero;

        public string RegionName { get; private set; }

        /// <summary>
        /// Gets or sets the duration that the item remains in the cache.
        /// </summary>
        public TimeSpan Expiration { get; set; }

        /// <summary>
        /// Gets or sets the span of time allowed before an item's expiration
        /// that will cause it to be re-expired when getting it from the cache.
        /// 
        /// For example, setting the Expiration to 10 minutes and the
        /// SlidingExpiration to 3 minutes means that the item must be accessed
        /// within the last 3 minutes to cause the expiration to be reset.
        /// 
        /// If it is desired to always re-expire the item when getting it from
        /// the cache, set this to the same value as the Expiration.
        /// 
        /// To emulate a sliding expiration policy siliar to ASP.NET's forms
        /// authentication, set this to half of the Expiration.
        /// 
        /// By defafult, no sliding expiration will occur (getting the item
        /// from the cache will not cause it to re-expire).
        /// </summary>
        public TimeSpan SlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the maximum duration that the item can be locked.
        /// </summary>
        public TimeSpan LockTimeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum duration to wait when acquiring a lock
        /// for the item. By default, this is the same as the lock timeout.
        /// </summary>
        public TimeSpan AcquireLockTimeout { get; set; }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="regionName"></param>
        public RedisCacheConfiguration(string regionName)
        {
            this.RegionName = regionName.ThrowIfNull("regionName");
            this.Expiration = DefaultExpiration;
            this.SlidingExpiration = NoSlidingExpiration;
            this.LockTimeout = DefaultLockTimeout;
            this.AcquireLockTimeout = DefaultAcquireLockTimeout;
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <param name="regionName"></param>
        /// <param name="other"></param>
        public RedisCacheConfiguration(string regionName, RedisCacheConfiguration other)
        {
            RegionName = regionName;
            Expiration = other.Expiration;
            SlidingExpiration = other.SlidingExpiration;
            LockTimeout = other.LockTimeout;
            AcquireLockTimeout = other.AcquireLockTimeout;
        }

        internal static RedisCacheConfiguration FromPropertiesOrDefaults(string regionName, IDictionary<string, string> properties)
        {
            var expiration = TimeSpan.FromSeconds(
                PropertiesHelper.GetInt32(Cfg.Environment.CacheDefaultExpiration, properties, (int)DefaultExpiration.TotalSeconds)
            );
            return new RedisCacheConfiguration(regionName)
            {
                Expiration = expiration
            };
        }

        internal void Validate()
        {
            if (SlidingExpiration < TimeSpan.Zero || SlidingExpiration > Expiration)
            {
                throw new ArgumentException(
                    String.Format("The sliding expiration '{0}' must be positive and cannot be greater than the expiration '{1}.",
                        SlidingExpiration,
                        Expiration
                    )
                );
            }
        }
    }
}
