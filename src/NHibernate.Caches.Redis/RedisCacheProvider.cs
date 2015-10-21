using System;
using System.Collections.Generic;
using System.Text;
using NHibernate.Cache;
using System.Configuration;
using StackExchange.Redis;
using System.Linq;

namespace NHibernate.Caches.Redis
{
    public class RedisCacheProvider : ICacheProvider
    {
        private static readonly IInternalLogger log = LoggerProvider.LoggerFor(typeof(RedisCacheProvider));
        private static ConnectionMultiplexer connectionMultiplexerStatic;
        private static RedisCacheProviderOptions optionsStatic;
        private static object syncRoot = new object();

        /// <summary>
        /// Set the <see cref="StackExchange.Redis.ConnectionMultiplexer"/> to be used to
        /// connect to Redis.
        /// </summary>
        /// <param name="connectionMultiplexer"></param>
        public static void SetConnectionMultiplexer(ConnectionMultiplexer connectionMultiplexer)
        {
            lock (syncRoot)
            {
                if (connectionMultiplexerStatic != null)
                {
                    throw new InvalidOperationException("The connection multiplexer can only be configured once.");
                }

                connectionMultiplexerStatic = connectionMultiplexer.ThrowIfNull();
            }
        }

        /// <summary>
        /// Set the options to be used to configure each cache.
        /// </summary>
        /// <param name="options"></param>
        public static void SetOptions(RedisCacheProviderOptions options)
        {
            lock (syncRoot)
            {
                if (optionsStatic != null)
                {
                    throw new InvalidOperationException("The options can only be configured once.");
                }

                optionsStatic = options.ThrowIfNull();
            }
        }

        internal static void InternalSetConnectionMultiplexer(ConnectionMultiplexer connectionMultiplexer)
        {
            connectionMultiplexerStatic = connectionMultiplexer;
        }

        internal static void InternalSetOptions(RedisCacheProviderOptions options)
        {
            optionsStatic = options;
        }

        public ICache BuildCache(string regionName, IDictionary<string, string> properties)
        {
            if (connectionMultiplexerStatic == null)
            {
                throw new InvalidOperationException(
                    "A 'ConnectionMultiplexer' must be configured with SetConnectionMultiplexer(). " + 
                    "For example, call 'RedisCacheProvider.SetConnectionMultiplexer(ConnectionMultiplexer.Connect(\"localhost:6379\"))' " +
                    "before creating the ISessionFactory."
                );
            }

            // Double-check so that we don't have to lock if necessary.
            if (optionsStatic == null)
            {
                lock (syncRoot)
                {
                    if (optionsStatic == null)
                    {
                        optionsStatic = new RedisCacheProviderOptions();
                    }
                }
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
                log.DebugFormat("building cache with region: {0}, properties: {1}", regionName, sb);
            }

            RedisCacheConfiguration configuration = null;

            if (!String.IsNullOrWhiteSpace(regionName) && optionsStatic.CacheConfigurations != null)
            {
                configuration = optionsStatic.CacheConfigurations.FirstOrDefault(x => x.RegionName == regionName);
            }

            if (configuration == null)
            {
                log.DebugFormat("loading cache configuration for '{0}' from properties/defaults", regionName);
                configuration = RedisCacheConfiguration.FromPropertiesOrDefaults(regionName, properties);
            }

            return BuildCache(configuration, properties, connectionMultiplexerStatic, optionsStatic);
        }

        protected virtual RedisCache BuildCache(RedisCacheConfiguration configuration, IDictionary<string, string> properties, ConnectionMultiplexer connectionMultiplexer, RedisCacheProviderOptions options)
        {
            return new RedisCache(configuration, connectionMultiplexer, options);
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
