using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NHibernate.Caches.Redis.Sample.Models
{
    public class BlogPost
    {
        public virtual int Id { get; set; }
        public virtual string Title { get; set; }
        public virtual string Body { get; set; }
        public virtual DateTime Created { get; set; }
    }
}