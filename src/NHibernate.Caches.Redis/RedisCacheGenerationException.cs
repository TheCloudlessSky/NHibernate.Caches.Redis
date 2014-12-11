using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    [Serializable]
    public class RedisCacheGenerationException : Exception
    {
        public RedisCacheGenerationException()
        {

        }

        public RedisCacheGenerationException(string message)
            : base(message)
        {

        }

        public RedisCacheGenerationException(string message, Exception inner)
            : base(message, inner)
        {

        }

        protected RedisCacheGenerationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }
    }
}
