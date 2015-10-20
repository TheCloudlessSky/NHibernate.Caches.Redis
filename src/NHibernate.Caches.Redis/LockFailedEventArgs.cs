using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public class LockFailedEventArgs
    {
        public string RegionName { get; private set; }
        public object Key { get; private set; }
        public string LockKey { get; private set; }
        public TimeSpan LockTimeout { get; private set; }
        public TimeSpan LockTakeTimeout { get; private set; }

        internal LockFailedEventArgs(string regionName, object key, string lockKey, TimeSpan lockTimeout, TimeSpan lockTakeTimeout)
        {
            this.RegionName = regionName;
            this.Key = key;
            this.LockKey = lockKey;
            this.LockTimeout = lockTimeout;
            this.LockTakeTimeout = lockTakeTimeout;
        }
    }
}
