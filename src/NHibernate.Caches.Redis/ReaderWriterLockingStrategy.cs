using System;
using System.Threading;

namespace NHibernate.Caches.Redis
{
    // From ServiceStack.Redis:
    // https://github.com/ServiceStack/ServiceStack.Redis/blob/v3/src/ServiceStack.Redis/Support/Locking/ReaderWriterLockingStrategy.cs
    internal class ReaderWriterLockingStrategy : ILockingStrategy
    {
        private readonly ReaderWriterLockSlim _lockObject = new ReaderWriterLockSlim();
        
        public IDisposable ReadLock()
        {
            return new ReadLock(_lockObject);
        }

        public IDisposable WriteLock()
        {
            return new WriteLock(_lockObject);
        }
    }
}