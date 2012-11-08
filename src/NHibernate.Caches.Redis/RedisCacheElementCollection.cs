using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace NHibernate.Caches.Redis
{
    [ConfigurationCollection(typeof(RedisCacheElementCollection),
            AddItemName = "cache",
            CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class RedisCacheElementCollection : ConfigurationElementCollection
    {
        public RedisCacheElement this[int index]
        {
            get { return (RedisCacheElement)BaseGet(index); }
            set
            {
                if (BaseGet(index) != null)
                {
                    BaseRemoveAt(index);
                }
                BaseAdd(index, value);
            }
        }

        public new RedisCacheElement this[string region]
        {
            get { return (RedisCacheElement)BaseGet(region); }
            set
            {
                if (BaseGet(region) != null)
                {
                    BaseRemove(region);
                }
                BaseAdd(value);
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new RedisCacheElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((RedisCacheElement)element).Region;
        }

        public void Add(RedisCacheElement element)
        {
            BaseAdd(element);
        }
    }
}
