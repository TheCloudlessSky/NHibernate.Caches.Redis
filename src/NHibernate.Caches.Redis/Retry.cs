using System;
using System.Threading;

namespace NHibernate.Caches.Redis
{
    internal static class Retry
    {
        private static readonly IInternalLogger log = LoggerProvider.LoggerFor(typeof(Retry));

        private const int intervalMilliseconds = 10;

        public static void UntilTrue(Func<bool> action, TimeSpan timeout)
        {
            var i = 0;
            var firstAttempt = DateTime.UtcNow;

            while (DateTime.UtcNow - firstAttempt < timeout)
            {
                if (action())
                {
                    return;
                }

                SleepBackOff(i);
                i++;
            }

            throw new TimeoutException(
                String.Format("Exceeded timeout of {0}", timeout)
            );
        }

        private static void SleepBackOff(int i)
        {
            var sleep = (i + 1) * intervalMilliseconds;
            
            if (log.IsDebugEnabled)
            {
                log.DebugFormat("Sleep back off for {0}ms", sleep);
            }

            Thread.Sleep(sleep);
        }
    }
}