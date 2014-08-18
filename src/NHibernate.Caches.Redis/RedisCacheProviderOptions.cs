using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheProviderOptions
    {
        // TODO: IGeneration
        // TODO: Region cache namespaces

        public IRedisCacheSerializer Serializer { get; set; }
        public Action<RedisCacheExceptionEventArgs> OnException { get; set; }

        public RedisCacheProviderOptions()
        {
            Serializer = new NetDataContractRedisCacheSerializer();
        }

        // Copy constructor.
        private RedisCacheProviderOptions(RedisCacheProviderOptions options)
        {
            Serializer = options.Serializer;
            OnException = options.OnException;
        }

        internal RedisCacheProviderOptions Clone()
        {
            return new RedisCacheProviderOptions(this);
        }
    }
}
