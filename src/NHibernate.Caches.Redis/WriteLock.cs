using System;
using System.Threading;

namespace NHibernate.Caches.Redis
{
    // From ServiceStack.Redis:
    // https://github.com/ServiceStack/ServiceStack.Redis/blob/v3/src/ServiceStack.Redis/Support/Locking/WriteLock.cs
    internal class WriteLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _lockObject;

        /// <summary>
        /// This class manages a write lock for a local readers/writer lock, 
        /// using the Resource Acquisition Is Initialization pattern
        /// </summary>
        /// <param name="lockObject"></param>
        public WriteLock(ReaderWriterLockSlim lockObject)
        {
            _lockObject = lockObject;
            lockObject.EnterWriteLock();
        }

        /// <summary>
        /// RAII disposal
        /// </summary>
        public void Dispose()
        {
            _lockObject.ExitWriteLock();
        }
    }
}