using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NHibernate.Caches.Redis.Tests
{
    [Serializable]
    public class Person
    {
        public virtual int Id { get; protected set; }
        public virtual int Age { get; set; }
        public virtual string Name { get; set; }

        protected Person() { }

        public Person(string name, int age)
        {
            this.Name = name;
            this.Age = age;
        }
    }
}
