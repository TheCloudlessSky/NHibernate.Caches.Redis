using System;
using System.Collections.Generic;
using System.Text;
using NHibernate.Cache;
using System.Configuration;
using StackExchange.Redis;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheProvider : ICacheProvider
    {
        private static readonly IInternalLogger Log = LoggerProvider.LoggerFor(typeof(RedisCacheProvider));
        private static ConnectionMultiplexer clientManagerStatic;
        private static readonly RedisCacheProviderSection Config;

        static RedisCacheProvider()
        {
            Config = ConfigurationManager.GetSection("nhibernateRedisCache") as RedisCacheProviderSection ??
                     new RedisCacheProviderSection();
        }

        public static void SetClientManager(ConnectionMultiplexer clientManager)
        {           
            if (clientManagerStatic != null)
                throw new InvalidOperationException("The client manager can only be configured once.");

            clientManagerStatic = clientManager.ThrowIfNull();
        }

        internal static void InternalSetClientManager(ConnectionMultiplexer clientManager)
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

            if (Log.IsDebugEnabled)
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
                Log.Debug("building cache with region: " + regionName + ", properties: " + sb);
            }

            RedisCacheElement configElement = null;
            if (!String.IsNullOrWhiteSpace(regionName))
            {
                configElement = Config.Caches[regionName];
            }

            return BuildCache(regionName, properties, configElement, clientManagerStatic);
        }

        protected virtual RedisCache BuildCache(string regionName, IDictionary<string, string> properties, RedisCacheElement configElement, ConnectionMultiplexer clientManager)
        {
            return new RedisCache(regionName, properties, configElement, clientManager);
        }

        public long NextTimestamp()
        {
            return Timestamper.Next();
        }

        public void Start(IDictionary<string, string> properties)
        {
            // No-op.
            Log.Debug("starting cache provider");
        }

        public void Stop()
        {
            // No-op.
            Log.Debug("stopping cache provider");
        }
    }
}
