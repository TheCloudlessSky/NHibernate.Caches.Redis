using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.Redis;

namespace NHibernate.Caches.Redis.Tests
{
    public class RedisTest : IDisposable
    {
        protected IRedisClientsManager ClientManager { get; private set; }
        protected IRedisClient Redis { get; private set; }
        protected IRedisNativeClient RedisNative { get { return (IRedisNativeClient)Redis; } }

        protected RedisTest()
        {
            this.ClientManager = new BasicRedisClientManager("localhost:6379");
            this.Redis = this.ClientManager.GetClient();
            this.Redis.FlushDb();
        }

        public void Dispose()
        {
            this.Redis.Dispose();
            this.ClientManager.Dispose();
        }
    }
}
