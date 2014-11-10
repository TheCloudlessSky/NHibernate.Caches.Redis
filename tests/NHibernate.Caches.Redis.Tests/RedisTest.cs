using System;
using StackExchange.Redis;

namespace NHibernate.Caches.Redis.Tests
{
    public class RedisTest : IDisposable
    {
        private const string connectionString = "localhost:6379,allowAdmin=true,abortConnect=false,syncTimeout=5000";
        protected const string InvalidHost = "unknown-host:6666,abortConnect=false";

        protected ConnectionMultiplexer ConnectionMultiplexer { get; private set; }
        protected IDatabase Redis { get; private set; }
        
        protected RedisTest()
        {
            ConnectionMultiplexer = ConnectionMultiplexer.Connect(connectionString);
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
