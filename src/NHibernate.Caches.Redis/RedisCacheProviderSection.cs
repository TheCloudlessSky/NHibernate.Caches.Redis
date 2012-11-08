using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheProviderSection : ConfigurationSection
    {
        [ConfigurationProperty("caches", IsDefaultCollection = false)]
        public RedisCacheElementCollection Caches
        {
            get
            {
                return (RedisCacheElementCollection)base["caches"];
            }
        }
    }
}
