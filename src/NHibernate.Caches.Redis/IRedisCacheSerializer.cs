using StackExchange.Redis;
namespace NHibernate.Caches.Redis
{
    public interface IRedisCacheSerializer
    {
        RedisValue Serialize(object value);
        object Deserialize(RedisValue value);
    }
}