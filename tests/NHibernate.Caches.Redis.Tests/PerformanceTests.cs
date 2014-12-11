using System;
using System.Collections.Generic;
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
                    object entityId = null;
                    UsingSession(sessionFactory, session =>
                    {
                        var entity = new Person("Foo", 1);
                        entityId = session.Save(entity);
                        session.Flush();
                        session.Clear();
                    
                        entity = session.Load<Person>(entityId);
                        entity.Name = Guid.NewGuid().ToString();
                        session.Flush();
                    });
                });
            });

            await Task.WhenAll(tasks);
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

            var tasks = sessionFactories.Select(sessionFactory =>
            {
                return Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        object entityId = null;
                        UsingSession(sessionFactory, session =>
                        {
                            var entity = new Person("Foo", 1);
                            entityId = session.Save(entity);
                            session.Flush();
                            session.Clear();

                            entity = session.Load<Person>(entityId);
                            entity.Name = Guid.NewGuid().ToString();
                            session.Flush();
                        });
                    }
                });
            });

            await Task.WhenAll(tasks);
        }
    }
}
