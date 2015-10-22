using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    [Serializable]
    public class RedisCacheException : Exception
    {
        public string RegionName { get; private set; }

        public RedisCacheException()
        {

        }

        public RedisCacheException(string message)
            : base(message)
        {

        }

        public RedisCacheException(string message, Exception inner)
            : base(message, inner)
        {

        }

        protected RedisCacheException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

        public RedisCacheException(string regionName, string message, Exception innerException)
            : this(message, innerException)
        {
            this.RegionName = regionName;
        }
    }
}
