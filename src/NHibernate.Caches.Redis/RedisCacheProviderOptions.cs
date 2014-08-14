using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheProviderOptions
    {
        // TODO: IGeneration
        // TODO: Region cache namespaces`

        public IRedisCacheSerializer Serializer { get; set; }

        public RedisCacheProviderOptions()
        {
            Serializer = new XmlObjectSerializerRedisCacheSerializer();
        }

        // TODO: Copy constructor.
        private RedisCacheProviderOptions(RedisCacheProviderOptions options)
        {
            Serializer = options.Serializer;
        }

        internal RedisCacheProviderOptions Clone()
        {
            return new RedisCacheProviderOptions(this);
        }
    }
}
