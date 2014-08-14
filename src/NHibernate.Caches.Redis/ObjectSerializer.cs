using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace NHibernate.Caches.Redis
{
    public class ObjectSerializer : ISerializer
    {
        protected readonly BinaryFormatter Bf = new BinaryFormatter();

        public virtual byte[] Serialize(object value)
        {
            if (value == null)
                return null;
            var memoryStream = new MemoryStream();
            memoryStream.Seek(0, 0);
            Bf.Serialize(memoryStream, value);
            return memoryStream.ToArray();
        }

        public virtual object Deserialize(byte[] someBytes)
        {
            if (someBytes == null)
                return null;
            var memoryStream = new MemoryStream();
            memoryStream.Write(someBytes, 0, someBytes.Length);
            memoryStream.Seek(0, 0);
            var de = Bf.Deserialize(memoryStream);
            return de;
        }
    }
}