using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public interface ILockTakeRetryStrategy
    {
        /// <summary>
        /// Gets a delegate that is used to determine if taking a lock should be retried.
        /// This must be thread-safe.
        /// </summary>
        /// <returns></returns>
        ShouldRetryLockTake GetShouldRetry();
    }
}
