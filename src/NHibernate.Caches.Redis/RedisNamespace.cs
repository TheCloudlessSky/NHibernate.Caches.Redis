using System;
using System.Threading;
namespace NHibernate.Caches.Redis
{
    // Derived from ServiceStack.Redis:
    // https://github.com/ServiceStack/ServiceStack.Redis/blob/v3/src/ServiceStack.Redis/Support/RedisNamespace.cs
    internal class RedisNamespace : IDisposable
    {
        private readonly ReaderWriterLockSlim locker = new ReaderWriterLockSlim();

        private readonly string prefix;
        private readonly string setOfKeysKey;
        private readonly string generationKey;

        private long generation = -1;

        public RedisNamespace(string prefix)
        {
            this.prefix = prefix;
            this.setOfKeysKey = prefix + ":keys";
            this.generationKey = prefix + ":generation";
        }

        public long GetGeneration()
        {
            try
            {
                locker.EnterReadLock();
                return generation;
            }
            finally
            {
                locker.ExitReadLock();
            }
        }

        public void SetGeneration(long newGeneration)
        {
            if (newGeneration < 0) return;

            try
            {
                locker.EnterWriteLock();

                if (generation == -1 || newGeneration > generation)
                {
                    generation = newGeneration;
                }
            }
            finally
            {
                locker.ExitWriteLock();
            }
        }

        public string GetSetOfKeysKey()
        {
            return setOfKeysKey;
        }

        public string GetGenerationKey()
        {
            return generationKey;
        }

        public string GetKey(object key)
        {
            return prefix + ":v" + generation + ":" + key;
        }

        public string GetLockKey(object key)
        {
            return GetKey(key) + ":lock";
        }

        public void Dispose()
        {
            locker.Dispose();
        }
    }
}