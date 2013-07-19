NHibernate.Caches.Redis
=======================

This is a [Redis](http://redis.io/) based [ICacheProvider](http://www.nhforge.org/doc/nh/en/#configuration-optional-cacheprovider) 
for [NHibernate](http://nhforge.org/) written in C# using [ServiceStack.Redis](https://github.com/ServiceStack/ServiceStack.Redis).

Installation
------------

1. You can install using NuGet: `PM> Install-Package NHibernate.Caches.Redis`
2. Or build/install from source: `msbuild .\build\build.proj` and then look
   inside the `bin` directory.

Usage
-----

Configure NHibernate to use the custom cache provider:

```xml
<property name="cache.use_second_level_cache">true</property>
<property name="cache.use_query_cache">true</property>
<property name="cache.provider_class">NHibernate.Caches.Redis.RedisCacheProvider, 
    NHibernate.Caches.Redis</property>
```

Set the `IRedisClientsManager` (pooled, basic, etc) on the `RedisCacheProvider`
*before* creating your `ISessionFactory`:

```csharp
// Or use your IoC container to wire this up.
var clientManager = new PooledRedisClientManager("localhost:6379");
RedisCacheProvider.SetClientManager(clientManager);

using (var sessionFactory = ...)
{
    // ...
}

clientManager.Dispose();
```

Configuration
-------------

Inside of the `app/web.config`, a custom configuration section can be added to
configure each cache region:

```xml
<configSections>
  <section name="nhibernateRedisCache" type="NHibernate.Caches.Redis.RedisCacheProviderSection, NHibernate.Caches.Redis" />
</configSections>

<nhibernateRedisCache>
  <caches>
    <cache region="BlogPost" expiration="900" />
  </caches>
</nhibernateRedisCache>
```

Changelog
---------

**1.2.1**
- Update ServiceStack.Redis to 3.9.55.

**1.2.0**
- Allow the provider to gracefully continue when Redis is unavailable.
- Fix infinite loop when data in Redis was cleared.

**1.1.0**
- Added configuration section for customizing the cache regions.
- Added sample project.

**1.0.0**
- Initial release.

---

Happy caching!
