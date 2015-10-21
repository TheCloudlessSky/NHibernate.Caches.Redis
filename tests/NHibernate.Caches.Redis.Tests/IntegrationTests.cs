using System;
using Xunit;

namespace NHibernate.Caches.Redis.Tests
{
    public class IntegrationTests : IntegrationTestBase
    {
        [Fact]
        void Entity_cache()
        {
            using (var sf = CreateSessionFactory())
            {
                object personId = null;

                UsingSession(sf, session =>
                {
                    personId = session.Save(new Person("Foo", 1));

                    // Put occurs on the next fetch from the DB.
                    Assert.Equal(0, sf.Statistics.SecondLevelCacheHitCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCacheMissCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCachePutCount);
                });

                sf.Statistics.Clear();

                UsingSession(sf, session =>
                {
                    session.Get<Person>(personId);
                    Assert.Equal(1, sf.Statistics.SecondLevelCacheMissCount);
                    Assert.Equal(1, sf.Statistics.SecondLevelCachePutCount);
                });

                sf.Statistics.Clear();

                UsingSession(sf, session =>
                {
                    session.Get<Person>(personId);
                    Assert.Equal(1, sf.Statistics.SecondLevelCacheHitCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCacheMissCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCachePutCount);
                });
            }
        }

        [Fact]
        void SessionFactory_Dispose_should_not_clear_cache()
        {
            using (var sf = CreateSessionFactory())
            {
                UsingSession(sf, session =>
                {
                    session.Save(new Person("Foo", 10));
                });

                UsingSession(sf, session =>
                {
                    session.QueryOver<Person>()
                        .Cacheable()
                        .List();

                    Assert.Equal(1, sf.Statistics.QueryCacheMissCount);
                    Assert.Equal(1, sf.Statistics.SecondLevelCachePutCount);
                    Assert.Equal(1, sf.Statistics.QueryCachePutCount);
                });
            }

            using (var sf = CreateSessionFactory())
            {
                UsingSession(sf, session =>
                {
                    session.QueryOver<Person>()
                        .Cacheable()
                        .List();

                    Assert.Equal(1, sf.Statistics.SecondLevelCacheHitCount);
                    Assert.Equal(1, sf.Statistics.QueryCacheHitCount);
                    Assert.Equal(0, sf.Statistics.SecondLevelCachePutCount);
                    Assert.Equal(0, sf.Statistics.QueryCachePutCount);
                });
            }
        }
    }
}
