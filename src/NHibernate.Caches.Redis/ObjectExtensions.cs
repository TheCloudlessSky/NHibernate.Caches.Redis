using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NHibernate.Caches.Redis
{
    static class ObjectExtensions
    {
        public static T ThrowIfNull<T>(this T source)
            where T : class
        {
            if (source == null) throw new ArgumentNullException();
            return source;
        }

        public static T ThrowIfNull<T>(this T source, string paramName)
        {
            if (source == null) throw new ArgumentNullException(paramName);
            return source;
        }
    }
}
