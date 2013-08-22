using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheExceptionEventArgs
    {
        public Exception Exception { get; private set; }
        public bool Throw { get; set; }

        public RedisCacheExceptionEventArgs(Exception exception)
        {
            this.Exception = exception;
            this.Throw = false;
        }
    }
}
