using StackExchange.Redis;
namespace NHibernate.Caches.Redis
{
    public interface ICacheSerializer
    {
        RedisValue Serialize(object value);
        object Deserialize(RedisValue value);
    }
}