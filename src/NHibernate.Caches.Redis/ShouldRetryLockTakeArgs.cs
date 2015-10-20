using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public class ShouldRetryLockTakeArgs
    {
        public string RegionName { get; private set; }
        public object Key { get; private set; }
        public string LockKey { get; private set; }
        public string LockValue { get; private set; }
        public TimeSpan LockTimeout { get; private set; }
        public TimeSpan LockTakeTimeout { get; private set; }

        internal ShouldRetryLockTakeArgs(string regionName, object key, string lockKey, string lockValue, TimeSpan lockTimeout, TimeSpan lockTakeTimeout)
        {
            this.RegionName = regionName;
            this.Key = key;
            this.LockKey = lockKey;
            this.LockValue = lockValue;
            this.LockTimeout = lockTimeout;
            this.LockTakeTimeout = lockTakeTimeout;
        }
    }
}
