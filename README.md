NHibernate.Caches.Redis
=======================

This is a [Redis](http://redis.io/) based [ICacheProvider](http://www.nhforge.org/doc/nh/en/#configuration-optional-cacheprovider) 
for [NHibernate](http://nhforge.org/) written in C# using [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis).

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

Set the `ConnectionMultiplexer` on the `RedisCacheProvider`
*before* creating your `ISessionFactory`:

```csharp
// Or use your IoC container to wire this up.
var connectionMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");
RedisCacheProvider.SetConnectionMultiplexer(connectionMultiplexer);

using (var sessionFactory = ...)
{
    // ...
}

// When your application exits:
connectionMultiplexer.Dispose();
```

Check out the `NHibernate.Caches.Redis.Sample` project to learn more.

Options
-------

You can customize certain behavior with the `RedisCacheProvider.SetOptions(options)`
method. For example, you can control how objects are serialized into Redis. 
[Here is a JSON.NET `ICacheSerializer`](https://gist.github.com/TheCloudlessSky/f60d47ad2ca4dea72583) implementation. Once added to your project, you can then configure the options:

```csharp
var options = new RedisCacheProviderOptions()
{
    Serializer = new NhJsonCacheSerializer()
};
RedisCacheProvider.SetOptions(options);
```

Cache Region Configuration
--------------------------

Similar to other NHibernate cache providers, inside of the `app/web.config`, a 
custom configuration section can be added to configure each cache region. For 
example, this feature allows you to control the expiration for a specific class
that you cache.

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

Exception Handling
------------------

You may require that NHibernate gracefully continue to the database as if it
missed the cache when an exception occurs. For example, imagine if you are 
using NHibernate in a web project and your Redis server is unavailable. 
You may not want NHibernate to continue to timeout for *every* NHibernate
operation. You could do something similar to this:

```csharp
public class RequestRecoveryRedisCache : RedisCache
{
    public const string SkipNHibernateCacheKey = "__SkipNHibernateCache__";

    public RequestRecoveryRedisCache(string regionName, IDictionary<string, string> properties, RedisCacheElement element, ConnectionMultiplexer connectionMultiplexer, RedisCacheProviderOptions options)
        : base(regionName, properties, element, connectionMultiplexer, options)
    {

    }

    public override object Get(object key)
    {
        if (HasFailedForThisHttpRequest()) return null;
        return base.Get(key);
    }

    public override void Put(object key, object value)
    {
        if (HasFailedForThisHttpRequest()) return;
        base.Put(key, value);
    }

    public override void Remove(object key)
    {
        if (HasFailedForThisHttpRequest()) return;
        base.Remove(key);
    }

    public override void Clear()
    {
        if (HasFailedForThisHttpRequest()) return;
        base.Clear();
    }

    public override void Destroy()
    {
        if (HasFailedForThisHttpRequest()) return;
        base.Destroy();
    }

    public override void Lock(object key)
    {
        if (HasFailedForThisHttpRequest()) return;
        base.Lock(key);
    }

    public override void Unlock(object key)
    {
        if (HasFailedForThisHttpRequest()) return;
        base.Unlock(key);
    }

    private bool HasFailedForThisHttpRequest()
    {
        return HttpContext.Current.Items.Contains(SkipNHibernateCacheKey);
    }
}

public class RequestRecoveryRedisCacheProvider : RedisCacheProvider
{
    protected override RedisCache BuildCache(string regionName, IDictionary<string, string> properties, RedisCacheElement configElement, ConnectionMultiplexer connectionMultiplexer, RedisCacheProviderOptions options)
    {
        options.OnException = (e) =>
        {
            HttpContext.Current.Items[RequestRecoveryRedisCache.SkipNHibernateCacheKey] = true;
        };

        return new RequestRecoveryRedisCache(regionName, properties, configElement, connectionMultiplexer, options);
    }
}
```
Then, use `RequestRecoveryRedisCacheProvider` in your `web.config` settings.

StackExchange.Redis and Strong Naming
-------------------------------------

If one of your other libraries references `StackExchange.Redis.StrongName`, and
you're having trouble building, [you can use a build alias on the strongly named
reference](https://github.com/TheCloudlessSky/NHibernate.Caches.Redis/pull/11) 
to get things to play nice together.

Changelog
---------

**2.0.0**
- Switch the Redis library to [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) because of licensing changes with
ServiceStack.Redis. This obviously causes a few breaking changes with the
constructors.
- Introduce `RedisCacheProvider.SetOptions` (so that, for example, you don't 
need to subclass to override `OnException`).
- Allow the serializer to be customized by implementing `ICacheSerializer`
and setting the `Serializer` on the options. The default serializer uses the
`NetDataContractSerializer`.
- Customize which database the Redis connection uses with the `Database`
option
- The cache key no longer duplicates the region prefix. In previous
versions, caching an object with the type `MyApp.Models.Blog` and a region
prefix of `v2` would use the key `v2:NHibernate-Cache:v2.ProcedureFlow.Core.Models.User:keys`.
The key is now `v2:NHibernate-Cache:ProcedureFlow.Core.Models.User:keys`.
- Allow the lock value to be customized. This is useful if you want to store
  information such as what machine/process generated the lock to help with
  debugging.


**1.3.0**
- Add the `OnException` method for sub-classing the cache client and handling 
  exceptions.

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

Contributors
------------

@MattiasJakobsson and @Tazer for helping switch over to `StackExchange.Redis`.

Happy caching!
