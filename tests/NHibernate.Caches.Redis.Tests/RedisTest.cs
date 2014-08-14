using System;
using StackExchange.Redis;

namespace NHibernate.Caches.Redis.Tests
{
    public class RedisTest : IDisposable
    {
        protected const string ValidHost = "localhost:6379,allowAdmin=true,abortConnect=false";
        protected const string InvalidHost = "unknown-host:6666,abortConnect=false";

        protected ConnectionMultiplexer ConnectionMultiplexer { get; private set; }
        protected IDatabase Redis { get; private set; }
        
        protected RedisTest()
        {
            ConnectionMultiplexer = ConnectionMultiplexer.Connect(ValidHost);
            Redis = ConnectionMultiplexer.GetDatabase();
            FlushDb();
        }

        protected void FlushDb()
        {
            ConnectionMultiplexer.GetServer("localhost", 6379).FlushAllDatabases();
        }

        public void Dispose()
        {
            ConnectionMultiplexer.Dispose();
        }
    }
}
