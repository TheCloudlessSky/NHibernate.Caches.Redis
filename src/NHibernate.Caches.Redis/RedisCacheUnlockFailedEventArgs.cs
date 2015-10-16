using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheUnlockFailedEventArgs
    {
        public string RegionName { get; private set; }
        public object Key { get; private set; }
        public string LockKey { get; private set; }
        public string LockValue { get; private set; }

        public RedisCacheUnlockFailedEventArgs(string regionName, object key, string lockKey, string lockValue)
        {
            this.RegionName = regionName;
            this.Key = key;
            this.LockKey = lockKey;
            this.LockValue = lockValue;
        }
    }
}
