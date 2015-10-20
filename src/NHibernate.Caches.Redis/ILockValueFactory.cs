using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    /// <summary>
    /// Generate a value used for a lock. This is helpful if you want to identify
    /// where the lock was created from (such as including the machine name, process
    /// id and a random Guid). This type must be thread-safe.
    /// </summary>
    public interface ILockValueFactory
    {
        /// <summary>
        /// Gets a unique value for a lock.
        /// </summary>
        /// <returns></returns>
        string GetLockValue();
    }
}
