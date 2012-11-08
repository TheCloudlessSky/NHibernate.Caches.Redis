using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.ComponentModel;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheElement : ConfigurationElement
    {
        [ConfigurationProperty("region", IsRequired = true, IsKey = true)]
        public string Region
        {
            get { return (string)base["region"]; }
            set { base["region"] = value; }
        }

        [TypeConverter(typeof(TimeSpanSecondsConverter))]
        [ConfigurationProperty("expiration", DefaultValue = "300" /* 5 minutes */, IsRequired = true)]
        public TimeSpan Expiration
        {
            get { return (TimeSpan)base["expiration"]; }
            set { base["expiration"] = value; }
        }

        public RedisCacheElement()
        {

        }

        public RedisCacheElement(string region, TimeSpan expiration)
        {
            this.Region = region;
            this.Expiration = expiration;
        }
    }
}
