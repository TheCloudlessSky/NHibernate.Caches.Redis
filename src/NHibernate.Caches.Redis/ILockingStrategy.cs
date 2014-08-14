using System;

namespace NHibernate.Caches.Redis
{
    // From ServiceStack.Redis:
    // https://github.com/ServiceStack/ServiceStack.Redis/blob/v3/src/ServiceStack.Redis/Support/Locking/ILockingStrategy.cs
    internal interface ILockingStrategy
    {
        IDisposable ReadLock();

        IDisposable WriteLock();
    }
}