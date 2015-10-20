using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public delegate bool ShouldRetryLockTake(ShouldRetryLockTakeArgs args);
}
