using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public interface IAcquireLockRetryStrategy
    {
        /// <summary>
        /// Gets a delegate that is used to determine if acquiring a lock should be retried.
        /// Implementors should respect the AcquireLockTimeout argument of the delegate.
        /// This must be thread-safe.
        /// </summary>
        /// <returns></returns>
        ShouldRetryAcquireLock GetShouldRetry();
    }
}
