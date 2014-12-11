using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using StackExchange.Redis;
using Xunit;

namespace NHibernate.Caches.Redis.Tests
{
    public class PerformanceTests : IntegrationTestBase
    {
        [Fact]
        async Task concurrent_sessions()
        {
            DisableLogging();
            var options = CreateTestProviderOptions();
            // TODO: options.Serializer = new ProtoBufCacheSerializer();
            RedisCacheProvider.InternalSetOptions(options);

            const int iterations = 10000;
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
                    });

                    UsingSession(sessionFactory, session =>
                    {
                        var entity = session.Load<Person>(entityId);
                        Assert.NotNull(entity);
                    });
                });
            });

            await Task.WhenAll(tasks);
        }
    }
}
