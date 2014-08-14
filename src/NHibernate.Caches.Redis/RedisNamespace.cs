namespace NHibernate.Caches.Redis
{
    public class RedisNamespace
    {

        private const string UniqueCharacter = "?";

        //make reserved keys unique by tacking N of these to the beginning of the string
        private const string ReservedTag = "@" + UniqueCharacter + "@";

        //unique separator between namespace and key
        private const string NamespaceKeySeparator = "#" + UniqueCharacter + "#";

        //make non-static keys unique by tacking on N of these to the end of the string
        public const string KeyTag = "%" + UniqueCharacter + "%";

        public const string NamespaceTag = "!" + UniqueCharacter + "!";

        //remove any odd numbered runs of the UniqueCharacter character
        private const string Sanitizer = UniqueCharacter + UniqueCharacter;

        // namespace generation - when generation changes, namespace is slated for garbage collection
        private long _namespaceGeneration = -1;

        // key for namespace generation
        private readonly string _namespaceGenerationKey;

        //sanitized name for namespace (includes namespace generation)
        private readonly string _namespacePrefix;

        //reserved, unique name for meta entries for this namespace

        // key for set of all global keys in this namespace
        private readonly string _globalKeysKey;

        // key for list of keys slated for garbage collection
        public const string NamespacesGarbageKey = ReservedTag + "REDIS_NAMESPACES_GARBAGE";

        public const int NumTagsForKey = 0;
        public const int NumTagsForLockKey = 1;

        public RedisNamespace(string name)
        {
            _namespacePrefix = Sanitize(name);

            var namespaceReservedName = NamespaceTag + _namespacePrefix;

            _globalKeysKey = namespaceReservedName;

            //get generation
            _namespaceGenerationKey = namespaceReservedName + "_" + "generation";

            LockingStrategy = new ReaderWriterLockingStrategy();
        }
        /// <summary>
        /// get locking strategy
        /// </summary>
        public ILockingStrategy LockingStrategy
        {
            get;
            set;
        }
        /// <summary>
        /// get current generation
        /// </summary>
        /// <returns></returns>
        public long GetGeneration()
        {
            using (LockingStrategy.ReadLock())
            {
                return _namespaceGeneration;
            }
        }
        /// <summary>
        /// set new generation
        /// </summary>
        /// <param name="generation"></param>
        public void SetGeneration(long generation)
        {
            if (generation < 0) return;

            using (LockingStrategy.WriteLock())
            {
                if (_namespaceGeneration == -1 || generation > _namespaceGeneration)
                    _namespaceGeneration = generation;
            }
        }
        /// <summary>
        /// redis key for generation
        /// </summary>
        /// <returns></returns>
        public string GetGenerationKey()
        {
            return _namespaceGenerationKey;
        }

        /// <summary>
        /// get redis key that holds all namespace keys
        /// </summary>
        /// <returns></returns>
        public string GetGlobalKeysKey()
        {
            return _globalKeysKey;
        }
        /// <summary>
        /// get global cache key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GlobalCacheKey(object key)
        {
            return GlobalKey(key, NumTagsForKey);
        }

        public string GlobalLockKey(object key)
        {
            return GlobalKey(key, NumTagsForLockKey) + "LOCK";
        }

        /// <summary>
        /// get global key inside of this namespace
        /// </summary>
        /// <param name="key"></param>
        /// <param name="numUniquePrefixes">prefixes can be added for name deconfliction</param>
        /// <returns></returns>
        public string GlobalKey(object key, int numUniquePrefixes)
        {
            var rc = Sanitize(key);
            if (_namespacePrefix != null && !_namespacePrefix.Equals(""))
                rc = _namespacePrefix + "_" + GetGeneration() + NamespaceKeySeparator + rc;
            for (var i = 0; i < numUniquePrefixes; ++i)
                rc += KeyTag;
            return rc;
        }
        /// <summary>
        /// replace UniqueCharacter with its double, to avoid name clash
        /// </summary>
        /// <param name="dirtyString"></param>
        /// <returns></returns>
        private static string Sanitize(string dirtyString)
        {
            return dirtyString == null ? null : dirtyString.Replace(UniqueCharacter, Sanitizer);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dirtyString"></param>
        /// <returns></returns>
        private static string Sanitize(object dirtyString)
        {
            return Sanitize(dirtyString.ToString());
        }
    }
}