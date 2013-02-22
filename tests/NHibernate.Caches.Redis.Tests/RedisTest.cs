using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.Redis;

namespace NHibernate.Caches.Redis.Tests
{
    public class RedisTest : IDisposable
    {
        protected const string ValidHost = "localhost:6379";
        protected const string InvalidHost = "unknown-host:6666";

        protected IRedisClientsManager ClientManager { get; private set; }
        protected IRedisClient Redis { get; private set; }
        protected IRedisNativeClient RedisNative { get { return (IRedisNativeClient)Redis; } }
        
        protected RedisTest()
        {
            this.ClientManager = new BasicRedisClientManager(ValidHost);
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
