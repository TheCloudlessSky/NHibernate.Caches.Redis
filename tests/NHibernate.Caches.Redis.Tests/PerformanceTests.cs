using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NHibernate.Caches.Redis.Tests
{
    public class PerformanceTests : IntegrationTestBase
    {
        [Fact]
        async Task concurrent_sessions_with_reads_and_writes()
        {
            DisableLogging();

            const int iterations = 1000;
            var sessionFactory = CreateSessionFactory();

            var tasks = Enumerable.Range(0, iterations).Select(i =>
            {
                return Task.Run(() =>
                {
                    UsingSession(sessionFactory, session =>
                    {
                        var entity = new Person("Foo", 1);
                        var entityId = session.Save(entity);
                        session.Flush();
                        session.Clear();
                    
                        entity = session.Load<Person>(entityId);
                        entity.Name = Guid.NewGuid().ToString();
                        session.Flush();
                    });
                });
            });

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            await Task.WhenAll(tasks);

            stopwatch.Stop();
            Console.WriteLine("Took on average {0}ms per session", stopwatch.Elapsed.TotalMilliseconds / iterations);
        }

        [Fact]
        async Task concurrent_session_factories_with_reads_and_writes()
        {
            DisableLogging();

            const int sessionFactoryCount = 5;
            const int iterations = 1000;

            // Create factories on the same thread so we don't run into
            // concurrency issues with NHibernate.
            var sessionFactories = Enumerable.Range(0, sessionFactoryCount).Select(i =>
            {
                return CreateSessionFactory();
            });

            var tasks = sessionFactories.Select((sessionFactory, i) =>
            {
                return Task.Run(() =>
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    for (int j = 0; j < iterations; j++)
                    {
                        UsingSession(sessionFactory, session =>
                        {
                            var entity = new Person("Foo", 1);
                            var entityId = session.Save(entity);
                            session.Flush();
                            session.Clear();

                            entity = session.Load<Person>(entityId);
                            entity.Name = Guid.NewGuid().ToString();
                            session.Flush();
                        });
                    }

                    stopwatch.Stop();
                    return new { SessionFactoryId = i, Elapsed = stopwatch.Elapsed };
                });
            });

            var timings = await Task.WhenAll(tasks);

            foreach (var timing in timings)
            {
                Console.WriteLine("SessionFactory#{0} took on average {1}ms per session", timing.SessionFactoryId, timing.Elapsed.TotalMilliseconds / iterations);
            }
        }
    }
}
