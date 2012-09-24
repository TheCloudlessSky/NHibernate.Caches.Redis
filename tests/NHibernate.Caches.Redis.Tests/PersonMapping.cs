using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;

namespace NHibernate.Caches.Redis.Tests
{
    public class PersonMapping : ClassMap<Person>
    {
        public PersonMapping()
        {
            Table("Person");
            Id(x => x.Id);
            Map(x => x.Age);
            Map(x => x.Name);

            Cache.ReadWrite();
        }
    }
}
