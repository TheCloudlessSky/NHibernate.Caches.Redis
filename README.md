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

Set the `IRedisClientsManager` (pooled, basic, etc) on the `RedisCacheProvider`
before creating your `ISessionFactory`:

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