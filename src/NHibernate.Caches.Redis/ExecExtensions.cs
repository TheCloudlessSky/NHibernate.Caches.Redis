using System;
using System.Threading;

namespace NHibernate.Caches.Redis
{
    public static class ExecExtensions
    {
        public static void RetryUntilTrue(Func<bool> action, TimeSpan? timeOut)
        {
            var i = 0;
            var firstAttempt = DateTime.UtcNow;

            while (timeOut == null || DateTime.UtcNow - firstAttempt < timeOut.Value)
            {
                i++;
                if (action())
                    return;

                SleepBackOffMultiplier(i);
            }

            throw new TimeoutException(string.Format("Exceeded timeout of {0}", timeOut.Value));
        }

        private static void SleepBackOffMultiplier(int i)
        {
            var rand = new Random(Guid.NewGuid().GetHashCode());
            var nextTry = rand.Next(
                (int)Math.Pow(i, 2), (int)Math.Pow(i + 1, 2) + 1);

            Thread.Sleep(nextTry);
        }
    }
}