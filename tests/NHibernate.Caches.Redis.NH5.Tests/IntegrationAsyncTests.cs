using System;
using System.Threading.Tasks;
using Xunit;

namespace NHibernate.Caches.Redis.Tests
{

    public class IntegrationAsyncTests : IntegrationTestBase
    {
        [Fact]
        async Task Entity_cache()
        {
            using (var sf = CreateSessionFactory())
            {
                object personId = null;

                await UsingSessionAsync(sf, async session =>
                {
                    personId = await session.SaveAsync(new Person("Foo", 1));

                    Assert.Equal(0, sf.Statistics.SecondLevelCacheHitCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCacheMissCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCachePutCount);
                });

                sf.Statistics.Clear();

                await UsingSessionAsync(sf, async session =>
                {
                    await session.GetAsync<Person>(personId);

                    Assert.Equal(1, sf.Statistics.SecondLevelCacheMissCount);
                    Assert.Equal(1, sf.Statistics.SecondLevelCachePutCount);
                });

                sf.Statistics.Clear();

                await UsingSessionAsync(sf, async session =>
                {
                    await session.GetAsync<Person>(personId);

                    Assert.Equal(1, sf.Statistics.SecondLevelCacheHitCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCacheMissCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCachePutCount);

                });
            }
        }

        [Fact]
        async Task SessionFactory_Dispose_should_not_clear_cache()
        {
            using (var sf = CreateSessionFactory())
            {
                await UsingSessionAsync(sf, async session =>
                {
                    await session.SaveAsync(new Person("Foo", 10));
                });

                await UsingSessionAsync(sf, async session =>
                {
                    await session.QueryOver<Person>()
                        .Cacheable()
                        .ListAsync();

                    Assert.Equal(1, sf.Statistics.QueryCacheMissCount);
                    Assert.Equal(1, sf.Statistics.SecondLevelCachePutCount);
                    Assert.Equal(1, sf.Statistics.QueryCachePutCount);
                });
            }

            using (var sf = CreateSessionFactory())
            {
                await UsingSessionAsync(sf, async session =>
                {
                    await session.QueryOver<Person>()
                        .Cacheable()
                        .ListAsync();

                    Assert.Equal(1, sf.Statistics.SecondLevelCacheHitCount);
                    Assert.Equal(1, sf.Statistics.QueryCacheHitCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCachePutCount);
                    Assert.Equal(0, sf.Statistics.QueryCachePutCount);
                });
            }
        }

        async Task UsingSessionAsync(ISessionFactory sessionFactory, Func<ISession, Task> action)
        {
            using (var session = sessionFactory.OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                await action(session);
                await transaction.CommitAsync();
            }
        }
    }
}
