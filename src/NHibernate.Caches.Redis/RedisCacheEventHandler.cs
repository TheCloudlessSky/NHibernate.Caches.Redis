using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public delegate void RedisCacheEventHandler<TEventArgs>(RedisCache sender, TEventArgs e);
}
