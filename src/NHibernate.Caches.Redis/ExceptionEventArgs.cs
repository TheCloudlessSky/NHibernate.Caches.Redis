using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NHibernate.Caches.Redis
{
    public class ExceptionEventArgs
    {
        public Exception Exception { get; private set; }
        public bool Throw { get; set; }

        internal ExceptionEventArgs(Exception exception)
        {
            this.Exception = exception;
            this.Throw = false;
        }
    }
}
