namespace NHibernate.Caches.Redis
{
    public interface ISerializer
    {
        byte[] Serialize(object value);

        object Deserialize(byte[] someBytes);
    }
}