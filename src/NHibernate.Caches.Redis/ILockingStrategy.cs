using System;

namespace NHibernate.Caches.Redis
{
    public interface ILockingStrategy
    {
        IDisposable ReadLock();

        IDisposable WriteLock();
    }
}