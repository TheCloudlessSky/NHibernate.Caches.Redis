using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHibernate.Caches.Redis
{
    public class GuidLockValueFactory : ILockValueFactory
    {
        public string GetLockValue()
        {
            return "lock-" + Guid.NewGuid();            
        }
    }
}
