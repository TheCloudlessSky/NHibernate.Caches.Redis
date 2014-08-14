using System;

namespace NHibernate.Caches.Redis
{
    internal interface ILockingStrategy
    {
        IDisposable ReadLock();

        IDisposable WriteLock();
    }
}