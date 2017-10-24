using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NHibernate.Caches.Redis.Tests
{
    public class AsyncIntegrationTests : IntegrationTestBase
    {
        [Fact]
        async Task Entity_cache()
        {
            using (var sf = CreateSessionFactory())
            {
                object personId = null;

                await UsingSessionAsync(sf, async session =>
                {
                    personId = await session.SaveAsync(new Person("Foo", 1), CancellationToken.None);

                    // Put occurs on the next fetch from the DB.
                    Assert.Equal(0, sf.Statistics.SecondLevelCacheHitCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCacheMissCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCachePutCount);
                });

                sf.Statistics.Clear();

	            await UsingSessionAsync(sf, async session =>
                {
                    await session.GetAsync<Person>(personId, CancellationToken.None);
                    Assert.Equal(1, sf.Statistics.SecondLevelCacheMissCount);
                    Assert.Equal(1, sf.Statistics.SecondLevelCachePutCount);
                });

                sf.Statistics.Clear();

	            await UsingSessionAsync(sf, async session =>
                {
                    await session.GetAsync<Person>(personId, CancellationToken.None);
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
                    await session.SaveAsync(new Person("Foo", 10), CancellationToken.None);
                });

	            await UsingSessionAsync(sf, async session =>
                {
                    await session.QueryOver<Person>()
                        .Cacheable()
                        .ListAsync(CancellationToken.None);

                    Assert.Equal(1, sf.Statistics.QueryCacheMissCount);
                    Assert.Equal(1, sf.Statistics.SecondLevelCachePutCount);
                    Assert.Equal(1, sf.Statistics.QueryCachePutCount);
                });
            }

            using (var sf = CreateSessionFactory())
            {
                UsingSession(sf, async session =>
                {
                    await session.QueryOver<Person>()
                        .Cacheable()
                        .ListAsync(CancellationToken.None);

                    Assert.Equal(1, sf.Statistics.SecondLevelCacheHitCount);
                    Assert.Equal(1, sf.Statistics.QueryCacheHitCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCachePutCount);
                    Assert.Equal(0, sf.Statistics.QueryCachePutCount);
                });
            }
        }
    }
}
