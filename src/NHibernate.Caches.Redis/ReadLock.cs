using System;
using System.Threading;

namespace NHibernate.Caches.Redis
{
    /// <summary>
    /// This class manages a read lock for a local readers/writer lock, 
    /// using the Resource Acquisition Is Initialization pattern
    /// </summary>
    internal class ReadLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _lockObject;

        /// <summary>
        /// RAII initialization 
        /// </summary>
        /// <param name="lockObject"></param>
        public ReadLock(ReaderWriterLockSlim lockObject)
        {
            _lockObject = lockObject;
            lockObject.EnterReadLock();
        }

        /// <summary>
        /// RAII disposal
        /// </summary>
        public void Dispose()
        {
            _lockObject.ExitReadLock();
        }
    }
}