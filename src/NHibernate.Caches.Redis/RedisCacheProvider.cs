using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Cache;
using ServiceStack.Redis;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheProvider : ICacheProvider
    {
        private static readonly IInternalLogger log;
        private static IRedisClientsManager clientManagerStatic;

        static RedisCacheProvider()
        {
            log = LoggerProvider.LoggerFor(typeof(RedisCacheProvider));
        }

        public static void SetClientManager(IRedisClientsManager clientManager)
        {           
            if (clientManagerStatic != null)
            {
                throw new InvalidOperationException("The client manager can only be configured once.");
            }

            clientManagerStatic = clientManager.ThrowIfNull();
        }

        internal static void InternalSetClientManager(IRedisClientsManager clientManager)
        {
            clientManagerStatic = clientManager;
        }

        public ICache BuildCache(string regionName, IDictionary<string, string> properties)
        {
            if (clientManagerStatic == null)
            {
                throw new InvalidOperationException(
                    "An 'IRedisClientsManager' must be configured with SetClientManager(). " + 
                    "For example, call 'RedisCacheProvider(new PooledRedisClientManager())' " +
                    "before creating the ISessionFactory.");
            }

            if (log.IsDebugEnabled)
            {
                var sb = new StringBuilder();
                foreach (var pair in properties)
                {
                    sb.Append("name=");
                    sb.Append(pair.Key);
                    sb.Append("&value=");
                    sb.Append(pair.Value);
                    sb.Append(";");
                }
                log.Debug("building cache with region: " + regionName + ", properties: " + sb);
            }

            return new RedisCache(regionName, properties, clientManagerStatic);
        }

        public long NextTimestamp()
        {
            return Timestamper.Next();
        }

        public void Start(IDictionary<string, string> properties)
        {
            // No-op.
            log.Debug("starting cache provider");
        }

        public void Stop()
        {
            // No-op.
            log.Debug("stopping cache provider");
        }
    }
}
