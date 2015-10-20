using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public class ShouldRetryAcquireLockArgs
    {
        public string RegionName { get; private set; }
        public object Key { get; private set; }
        public string LockKey { get; private set; }
        public string LockValue { get; private set; }
        public TimeSpan LockTimeout { get; private set; }
        public TimeSpan AcquireLockTimeout { get; private set; }

        internal ShouldRetryAcquireLockArgs(string regionName, object key, string lockKey, string lockValue, TimeSpan lockTimeout, TimeSpan acquireLockTimeout)
        {
            this.RegionName = regionName;
            this.Key = key;
            this.LockKey = lockKey;
            this.LockValue = lockValue;
            this.LockTimeout = lockTimeout;
            this.AcquireLockTimeout = acquireLockTimeout;
        }
    }
}
