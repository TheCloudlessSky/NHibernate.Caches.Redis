using System;
using StackExchange.Redis;

namespace NHibernate.Caches.Redis.Tests
{
    public abstract class RedisTest : IDisposable
    {
        private const int testDb = 15;
        private const string testHost = "localhost";
        private const int testPort = 6379;
        private static readonly string connectionString = testHost + ":" + testPort + ",allowAdmin=true,abortConnect=false,syncTimeout=5000";

        protected ConnectionMultiplexer ConnectionMultiplexer { get; private set; }
        protected IDatabase Redis { get; private set; }
        
        protected RedisTest()
        {
            LoggerProvider.SetLoggersFactory(new OnlyRedisCacheLoggerFactory());
            EnableLogging();
            
            ConnectionMultiplexer = ConnectionMultiplexer.Connect(connectionString);
            Redis = GetDatabase();
            FlushDb();
        }

        protected RedisCacheProviderOptions CreateTestProviderOptions()
        {
            return new RedisCacheProviderOptions()
            {
                Database = testDb
            };
        }

        protected IDatabase GetDatabase()
        {
            return ConnectionMultiplexer.GetDatabase(testDb);
        }

        protected void FlushDb()
        {
            ConnectionMultiplexer.GetServer(testHost, testPort).FlushDatabase(testDb);
        }

        public void Dispose()
        {
            ConnectionMultiplexer.Dispose();
        }

        protected void EnableLogging()
        {
            ConsoleLogger.IsEnabled = true;
        }

        protected void DisableLogging()
        {
            ConsoleLogger.IsEnabled = false;
        }

        private class OnlyRedisCacheLoggerFactory : ILoggerFactory
        {
            public IInternalLogger LoggerFor(System.Type type)
            {
                if (type.Namespace.StartsWith(typeof(RedisCache).Namespace))
                {
                    return new ConsoleLogger();
                }
                return new NoLoggingInternalLogger();
            }

            public IInternalLogger LoggerFor(string keyName)
            {
                return new NoLoggingInternalLogger();
            }
        }

        private class ConsoleLogger : IInternalLogger
        {
            public static bool IsEnabled { get; set; }

            public bool IsDebugEnabled
            {
                get { return IsEnabled; }
            }

            public bool IsErrorEnabled
            {
                get { return IsEnabled; }
            }

            public bool IsFatalEnabled
            {
                get { return IsEnabled; }
            }

            public bool IsInfoEnabled
            {
                get { return IsEnabled; }
            }

            public bool IsWarnEnabled
            {
                get { return IsEnabled; }
            }

            public void Debug(object message, Exception exception)
            {
                if (!IsEnabled) return;

                Console.WriteLine("DEBUG: " + message + "\n\n" + exception.Message);
            }

            public void Debug(object message)
            {
                if (!IsEnabled) return;

                Console.WriteLine("DEBUG: " + message);                
            }

            public void DebugFormat(string format, params object[] args)
            {
                if (!IsEnabled) return;

                Console.WriteLine("DEBUG: " + format, args);                
            }

            public void Error(object message, Exception exception)
            {
                if (!IsEnabled) return;

                Console.WriteLine("ERROR: " + message + "\n\n" + exception.Message);
            }

            public void Error(object message)
            {
                if (!IsEnabled) return;

                Console.WriteLine("ERROR: " + message);
            }

            public void ErrorFormat(string format, params object[] args)
            {
                if (!IsEnabled) return;

                Console.WriteLine("ERROR: " + format, args);
            }

            public void Fatal(object message, Exception exception)
            {
                if (!IsEnabled) return;

                Console.WriteLine("FATAL: " + message + "\n\n" + exception.Message);
            }

            public void Fatal(object message)
            {
                if (!IsEnabled) return;

                Console.WriteLine("FATAL: " + message);
            }

            public void Info(object message, Exception exception)
            {
                if (!IsEnabled) return;

                Console.WriteLine("INFO: " + message + "\n\n" + exception.Message);
            }

            public void Info(object message)
            {
                if (!IsEnabled) return;

                Console.WriteLine("INFO: " + message);
            }

            public void InfoFormat(string format, params object[] args)
            {
                if (!IsEnabled) return;

                Console.WriteLine("INFO: " + format, args);
            }

            public void Warn(object message, Exception exception)
            {
                if (!IsEnabled) return;

                Console.WriteLine("WARN: " + message + "\n\n" + exception.Message);
            }

            public void Warn(object message)
            {
                if (!IsEnabled) return;

                Console.WriteLine("WARN: " + message);
            }

            public void WarnFormat(string format, params object[] args)
            {
                if (!IsEnabled) return;

                Console.WriteLine("WARN: " + format, args);
            }
        }
    }
}
