using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.Redis;
using System.Net.Sockets;

namespace NHibernate.Caches.Redis.Tests
{
    public class StoppableRedisClientManager : IRedisClientsManager
    {
        private readonly IRedisClientsManager wrappedClientManager;

        public bool Available { get; set; }

        public StoppableRedisClientManager(IRedisClientsManager wrappedClientManager)
        {
            this.wrappedClientManager = wrappedClientManager;
            this.Available = true;
        }

        public ServiceStack.CacheAccess.ICacheClient GetCacheClient()
        {
            if (Available)
            {
                return wrappedClientManager.GetCacheClient();
            }
            else
            {
                throw new SocketException();
            }
        }

        public IRedisClient GetClient()
        {
            if (Available)
            {
                return wrappedClientManager.GetClient();
            }
            else
            {
                throw new SocketException();
            }
        }

        public ServiceStack.CacheAccess.ICacheClient GetReadOnlyCacheClient()
        {
            if (Available)
            {
                return wrappedClientManager.GetReadOnlyCacheClient();
            }
            else
            {
                throw new SocketException();
            }
        }

        public IRedisClient GetReadOnlyClient()
        {
            if (Available)
            {
                return wrappedClientManager.GetReadOnlyClient();
            }
            else
            {
                throw new SocketException();
            }
        }

        public void Dispose()
        {
            wrappedClientManager.Dispose();
        }
    }
}
