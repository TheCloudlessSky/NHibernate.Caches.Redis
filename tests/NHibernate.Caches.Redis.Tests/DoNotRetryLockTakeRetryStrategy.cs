using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis.Tests
{
    public class DoNotRetryLockTakeRetryStrategry : ILockTakeRetryStrategy
    {
        public ShouldRetryLockTake GetShouldRetry()
        {
            return e => false;
        }
    }
}
