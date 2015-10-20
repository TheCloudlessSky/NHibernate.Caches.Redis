using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public enum RedisCacheMethod
    {
        Unknown = 0,
        Put,
        Get,
        Remove,
        Clear,
        Lock,
        Unlock
    }
}
