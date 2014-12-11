using System;
using System.IO;
using NHibernate.Cache;
using NHibernate.Cache.Entry;
using ProtoBuf;
using ProtoBuf.Meta;
using StackExchange.Redis;
using Xunit;

namespace NHibernate.Caches.Redis.Tests
{
    public class IntegrationTests : IntegrationTestBase
    {

        private class ProtoBufCacheSerializer : ICacheSerializer
        {
            private static readonly RuntimeTypeModel typeModel;

            static ProtoBufCacheSerializer()
            {
                typeModel = TypeModel.Create();

                typeModel.Add(typeof(CachedItem), applyDefaultBehaviour: false).SetSurrogate(typeof(CachedItemSurrogate));
                typeModel.Add(typeof(CacheEntry), applyDefaultBehaviour: false).SetSurrogate(typeof(CacheEntrySurrogate));

                //typeModel.AutoCompile = false;

                //AddTypeMapSurrogate<Int32>();
                //AddTypeMapSurrogate<Int64>();

                // TODO...


                //typeModel.CompileInPlace();

                // TODO: What else...

                // TODO: Compile and freeze.
            }

            //private static void AddTypeMapSurrogate<T>()
            //{
            //    //typeModel.Add(typeof(object), applyDefaultBehaviour: false).AddSubType(1, typeof(T));

            //    typeModel.Add(typeof(Surrogate<T>), applyDefaultBehaviour: false).Add("Value");

            //    //typeModel.Add(typeof(T), applyDefaultBehaviour: false).SetSurrogate(typeof(Surrogate<T>));
            //}

            [ProtoContract]
            private class Surrogate<T>
            {
                [ProtoMember(1)]
                internal T Value { get; set; }

                public Surrogate(T value)
                {
                    this.Value = value;
                }

                public static Surrogate<T> Cast(object value)
                {
                    var typedValue = (T)value;
                    return new Surrogate<T>(typedValue);
                }
            }

            [ProtoContract]
            private class CachedItemSurrogate
            {
                [ProtoMember(1, DynamicType = true)]
                public object Value { get; set; }

                [ProtoMember(2)]
                public long FreshTimestamp { get; set; }

                [ProtoMember(3, DynamicType = true)]
                public object Version { get; set; }

                public static implicit operator CachedItem(CachedItemSurrogate surrogate)
                {
                    return new CachedItem(surrogate.Value, surrogate.FreshTimestamp, surrogate.Version);
                }

                public static implicit operator CachedItemSurrogate(CachedItem original)
                {
                    return new CachedItemSurrogate()
                    {
                        Value = original.Value,
                        FreshTimestamp = original.FreshTimestamp,
                        // TODO: Cache this...
                        Version = typeof(CachedItem).GetField("version", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(original)
                    };
                }
            }

            [ProtoContract]
            private class CacheEntrySurrogate
            {
                [ProtoMember(1, DynamicType = true)]
                public object[] DisassembledState { get; set; }

                [ProtoMember(2)]
                public string Subclass { get; set; }

                [ProtoMember(3)]
                public bool ArePropertiesUnfetched { get; set; }

                [ProtoMember(4, DynamicType = true)]
                public object Version { get; set; }

                public static implicit operator CacheEntry(CacheEntrySurrogate surrogate)
                {
                    // TODO: Cache this...
                    var contructor = typeof(CacheEntry).GetConstructor(new[] { typeof(object[]), typeof(string), typeof(bool), typeof(object) });

                    var result = (CacheEntry)contructor.Invoke(new[] { surrogate.DisassembledState, surrogate.Subclass, surrogate.ArePropertiesUnfetched, surrogate.Version });
                    return result;
                }

                public static implicit operator CacheEntrySurrogate(CacheEntry original)
                {
                    return new CacheEntrySurrogate()
                    {
                        DisassembledState = original.DisassembledState,
                        Subclass = original.Subclass,
                        ArePropertiesUnfetched = original.AreLazyPropertiesUnfetched,
                        Version = original.Version
                    };
                }
            }

            // TODO: Continue with these...
            private enum SurrogateType : byte
            {
                Dynamic = 0,
                Boolean = 1,
                Byte = 2,
                SByte = 3,
                Int16 = 4,
                UInt16 = 5,
                Int32 = 6,
                UInt32 = 7,
                Int64 = 8,
                UInt64 = 9,
                IntPtr = 10,
                UIntPtr = 11,
                Char = 12,
                Double = 13,
                Single = 14
            }

            [ProtoContract]
            private class DynamicSurrogate
            {
                [ProtoMember(1, DynamicType = true)]
                public object Value { get; set; }
            }

            public RedisValue Serialize(object value)
            {
                //using (var stream = new MemoryStream())
                //{
                //    var wrapper = new DynamicWrapper() { Value = value };
                //    typeModel.Serialize(stream, wrapper);

                //    var buffer = stream.ToArray();
                //    return buffer;
                //}

                using (var stream = new MemoryStream())
                {
                    if (value is Int32)
                    {
                        stream.Write(new[] { (byte)SurrogateType.Int32 }, 0, 1);
                        typeModel.Serialize(stream, Surrogate<Int32>.Cast(value));
                    }
                    else if (value is Int64)
                    {
                        stream.Write(new[] { (byte)SurrogateType.Int64 }, 0, 1);
                        typeModel.Serialize(stream, Surrogate<Int64>.Cast(value));
                    }
                    else
                    {
                        stream.Write(new[] { (byte)SurrogateType.Dynamic }, 0, 1);
                        typeModel.Serialize(stream, new DynamicSurrogate() { Value = value });
                    }

                    var result = stream.ToArray();
                    return result;
                }
            }

            public object Deserialize(RedisValue value)
            {
                //using (var stream = new MemoryStream(value))
                //{
                //    var wrapper = (DynamicSurrogate)typeModel.Deserialize(stream, null, typeof(DynamicSurrogate));

                //    if (wrapper == null)
                //    {
                //        return null;
                //    }
                //    else
                //    {
                //        return wrapper.Value;
                //    }
                //}

                if (value.IsNull) return null;

                using (var stream = new MemoryStream(value))
                {
                    var surrogateTypeBuffer = new byte[1];
                    var bytesRead = stream.Read(surrogateTypeBuffer, 0, 1);

                    // Could not determine the wrapper type.
                    if (bytesRead == 0) return null;

                    var surrogateType = (SurrogateType)surrogateTypeBuffer[0];

                    if (surrogateType == SurrogateType.Int32)
                    {
                        var int32Result = (Surrogate<Int32>)typeModel.Deserialize(stream, null, typeof(Surrogate<Int32>));
                        return int32Result.Value;
                    }
                    else if (surrogateType == SurrogateType.Int64)
                    {
                        var int64Result = (Surrogate<Int64>)typeModel.Deserialize(stream, null, typeof(Surrogate<Int64>));
                        return int64Result.Value;
                    }
                    else
                    {
                        // TODO: Explain we need it to include type information for deserialize.
                        var dynamicResult = (DynamicSurrogate)typeModel.Deserialize(stream, null, typeof(DynamicSurrogate));
                        return dynamicResult.Value;
                    }
                }
            }
        }

        [Fact]
        void Entity_cache()
        {
            var options = CreateTestProviderOptions();
            options.Serializer = new ProtoBufCacheSerializer();
            RedisCacheProvider.InternalSetOptions(options);

            using (var sf = CreateSessionFactory())
            {
                object personId = null;

                UsingSession(sf, session =>
                {
                    personId = session.Save(new Person("Foo", 1));
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
