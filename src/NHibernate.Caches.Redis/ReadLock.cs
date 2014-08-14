using System;
using System.Threading;

namespace NHibernate.Caches.Redis
{
    // From ServiceStack.Redis:
    // https://github.com/ServiceStack/ServiceStack.Redis/blob/v3/src/ServiceStack.Redis/Support/Locking/ReadLock.cs
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