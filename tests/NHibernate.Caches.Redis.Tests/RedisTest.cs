using System;
using StackExchange.Redis;

namespace NHibernate.Caches.Redis.Tests
{
    public class RedisTest : IDisposable
    {
        private const string connectionString = "localhost:6379,allowAdmin=true,abortConnect=false,syncTimeout=5000";

        protected ConnectionMultiplexer ConnectionMultiplexer { get; private set; }
        protected IDatabase Redis { get; private set; }
        
        protected RedisTest()
        {
            LoggerProvider.SetLoggersFactory(new RedisCacheLoggerFactory());

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

        private class RedisCacheLoggerFactory : ILoggerFactory
        {
            public IInternalLogger LoggerFor(System.Type type)
            {
                if (type == typeof(RedisCache))
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
            public bool IsDebugEnabled
            {
                get { return true; }
            }

            public bool IsErrorEnabled
            {
                get { return true; }
            }

            public bool IsFatalEnabled
            {
                get { return true; }
            }

            public bool IsInfoEnabled
            {
                get { return true; }
            }

            public bool IsWarnEnabled
            {
                get { return true; }
            }

            public void Debug(object message, Exception exception)
            {
                Console.WriteLine("DEBUG: " + message + "\n\n" + exception.Message);
            }

            public void Debug(object message)
            {
                Console.WriteLine("DEBUG: " + message);                
            }

            public void DebugFormat(string format, params object[] args)
            {
                Console.WriteLine("DEBUG: " + format, args);                
            }

            public void Error(object message, Exception exception)
            {
                Console.WriteLine("ERROR: " + message + "\n\n" + exception.Message);
            }

            public void Error(object message)
            {
                Console.WriteLine("ERROR: " + message);
            }

            public void ErrorFormat(string format, params object[] args)
            {
                Console.WriteLine("ERROR: " + format, args);
            }

            public void Fatal(object message, Exception exception)
            {
                Console.WriteLine("FATAL: " + message + "\n\n" + exception.Message);
            }

            public void Fatal(object message)
            {
                Console.WriteLine("FATAL: " + message);
            }

            public void Info(object message, Exception exception)
            {
                Console.WriteLine("INFO: " + message + "\n\n" + exception.Message);
            }

            public void Info(object message)
            {
                Console.WriteLine("INFO: " + message);
            }

            public void InfoFormat(string format, params object[] args)
            {
                Console.WriteLine("INFO: " + format, args);
            }

            public void Warn(object message, Exception exception)
            {
                Console.WriteLine("WARN: " + message + "\n\n" + exception.Message);
            }

            public void Warn(object message)
            {
                Console.WriteLine("WARN: " + message);
            }

            public void WarnFormat(string format, params object[] args)
            {
                Console.WriteLine("WARN: " + format, args);
            }
        }
    }
}
