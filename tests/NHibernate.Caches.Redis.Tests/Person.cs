using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace NHibernate.Caches.Redis.Tests
{
    [Serializable]
    [DataContract]
    public class Person
    {
        [DataMember]
        public virtual int Id { get; set; }
        [DataMember]
        public virtual int Age { get; set; }
        [DataMember]
        public virtual string Name { get; set; }

        protected Person() { }

        public Person(string name, int age)
        {
            this.Name = name;
            this.Age = age;
        }
    }
}
