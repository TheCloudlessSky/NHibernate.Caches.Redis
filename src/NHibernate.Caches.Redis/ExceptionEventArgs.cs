using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NHibernate.Caches.Redis
{
    public class ExceptionEventArgs
    {
        public string RegionName { get; private set; }
        public Exception Exception { get; private set; }
        public RedisCacheMethod Method { get; private set; }
        public bool Throw { get; set; }

        internal ExceptionEventArgs(string regionName, RedisCacheMethod method, Exception exception)
        {
            this.RegionName = regionName;
            this.Exception = exception;
            this.Method = method;
            this.Throw = false;
        }
    }
}
