using System;
using System.Threading;
namespace NHibernate.Caches.Redis
{
    internal class RedisNamespace
    {
        private readonly string prefix;
        private readonly string setOfActiveKeysKey;

        public RedisNamespace(string prefix)
        {
            this.prefix = prefix;
            this.setOfActiveKeysKey = prefix + ":keys";
        }

        public string GetSetOfActiveKeysKey()
        {
            return setOfActiveKeysKey;
        }

        public string GetKey(object key)
        {
            return prefix + ":" + key;
        }

        public string GetLockKey(object key)
        {
            return GetKey(key) + ":lock";
        }
    }
}