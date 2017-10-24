using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NHibernate.Caches.Redis
{
	public partial class RedisCache
	{
		public async Task<object> GetAsync(object key, CancellationToken cancellationToken)
		{
			key.ThrowIfNull();

			log.DebugFormat("get from cache: regionName='{0}', key='{1}'", RegionName, key);

			try
			{
				var cacheKey = CacheNamespace.GetKey(key);
				var setOfActiveKeysKey = CacheNamespace.GetSetOfActiveKeysKey();

				var db = GetDatabase();

				var resultValues = (RedisValue[])await db.ScriptEvaluateAsync(getScript, new
				{
					key = cacheKey,
					setOfActiveKeysKey = setOfActiveKeysKey
				});

				if (resultValues[0].IsNullOrEmpty)
				{
					log.DebugFormat("cache miss: regionName='{0}', key='{1}'", RegionName, key);
					return null;
				}
				else
				{
					var serializedResult = resultValues[0];

					var deserializedValue = options.Serializer.Deserialize(serializedResult);

					if (deserializedValue != null && slidingExpiration != RedisCacheConfiguration.NoSlidingExpiration)
					{
						await db.ScriptEvaluateAsync(slidingExpirationScript, new
						{
							key = cacheKey,
							expiration = expiration.TotalMilliseconds,
							slidingExpiration = slidingExpiration.TotalMilliseconds
						}, fireAndForgetFlags);
					}

					return deserializedValue;
				}
			}
			catch (Exception e)
			{
				HandleGetException(key, e);

				return null;
			}
		}

		public async Task PutAsync(object key, object value, CancellationToken cancellationToken)
		{
			key.ThrowIfNull("key");
			value.ThrowIfNull("value");

			log.DebugFormat("put in cache: regionName='{0}', key='{1}'", RegionName, key);

			try
			{
				var serializedValue = options.Serializer.Serialize(value);

				var cacheKey = CacheNamespace.GetKey(key);
				var setOfActiveKeysKey = CacheNamespace.GetSetOfActiveKeysKey();
				var db = GetDatabase();

				await db.ScriptEvaluateAsync(putScript, new
				{
					key = cacheKey,
					setOfActiveKeysKey = setOfActiveKeysKey,
					value = serializedValue,
					expiration = expiration.TotalMilliseconds
				}, fireAndForgetFlags);
			}
			catch (Exception e)
			{
				HandlePutException(key, e);
			}
		}

		public async Task RemoveAsync(object key, CancellationToken cancellationToken)
		{
			key.ThrowIfNull();

			log.DebugFormat("remove from cache: regionName='{0}', key='{1}'", RegionName, key);

			try
			{
				var cacheKey = CacheNamespace.GetKey(key);
				var setOfActiveKeysKey = CacheNamespace.GetSetOfActiveKeysKey();
				var db = GetDatabase();

				await db.ScriptEvaluateAsync(removeScript, new
				{
					key = cacheKey,
					setOfActiveKeysKey = setOfActiveKeysKey
				}, fireAndForgetFlags);
			}
			catch (Exception e)
			{
				HandleRemoveException(key, e);
			}
		}

		public async Task ClearAsync(CancellationToken cancellationToken)
		{
			log.DebugFormat("clear cache: regionName='{0}'", RegionName);

			try
			{
				var setOfActiveKeysKey = CacheNamespace.GetSetOfActiveKeysKey();
				var db = GetDatabase();
				await db.KeyDeleteAsync(setOfActiveKeysKey, fireAndForgetFlags);
			}
			catch (Exception e)
			{
				HandleClearException(e);
			}
		}

		public async Task LockAsync(object key, CancellationToken cancellationToken)
		{
			log.DebugFormat("acquiring cache lock: regionName='{0}', key='{1}'", RegionName, key);

			try
			{
				var lockKey = CacheNamespace.GetLockKey(key);
				var shouldRetry = options.AcquireLockRetryStrategy.GetShouldRetry();

				var wasLockAcquired = false;
				var shouldTryAcquireLock = true;

				while (shouldTryAcquireLock)
				{
					var lockData = new LockData(
						key: Convert.ToString(key),
						lockKey: lockKey,
						// Recalculated each attempt to ensure a unique value.
						lockValue: options.LockValueFactory.GetLockValue()
					);

					if (await TryAcquireLockAsync(lockData))
					{
						wasLockAcquired = true;
						shouldTryAcquireLock = false;
					}
					else
					{
						var shouldRetryArgs = new ShouldRetryAcquireLockArgs(
							RegionName, lockData.Key, lockData.LockKey,
							lockData.LockValue, lockTimeout, acquireLockTimeout
						);
						shouldTryAcquireLock = shouldRetry(shouldRetryArgs);
					}
				}

				if (!wasLockAcquired)
				{
					var lockFailedArgs = new LockFailedEventArgs(
						RegionName, key, lockKey,
						lockTimeout, acquireLockTimeout
					);
					options.OnLockFailed(this, lockFailedArgs);
				}
			}
			catch (Exception e)
			{
				HandleLockException(key, e);
			}
		}

		public async Task UnlockAsync(object key, CancellationToken cancellationToken)
		{
			// Use Remove() instead of Get() because we are releasing the lock
			// anyways.
			var lockData = acquiredLocks.Remove(Convert.ToString(key)) as LockData;
			if (lockData == null)
			{
				log.WarnFormat("attempted to unlock '{0}' but a previous lock was not acquired or timed out", key);
				var unlockFailedEventArgs = new UnlockFailedEventArgs(
					RegionName, key, lockKey: null, lockValue: null
				);
				options.OnUnlockFailed(this, unlockFailedEventArgs);
				return;
			}

			log.DebugFormat("releasing cache lock: regionName='{0}', key='{1}', lockKey='{2}', lockValue='{3}'",
				RegionName, lockData.Key, lockData.LockKey, lockData.LockValue
			);

			try
			{
				var db = GetDatabase();

				// Don't use IDatabase.LockRelease() because it uses watch/unwatch
				// where we prefer an atomic operation (via a script).
				var wasLockReleased = (bool)await db.ScriptEvaluateAsync(unlockScript, new
				{
					lockKey = lockData.LockKey,
					lockValue = lockData.LockValue
				});

				if (!wasLockReleased)
				{
					log.WarnFormat("attempted to unlock '{0}' but it could not be released (it maybe timed out or was cleared in Redis)", lockData);

					var unlockFailedEventArgs = new UnlockFailedEventArgs(
						RegionName, key, lockData.LockKey, lockData.LockValue
					);
					options.OnUnlockFailed(this, unlockFailedEventArgs);
				}
			}
			catch (Exception e)
			{
				HandleUnlockException(lockData, e);
			}
		}

		private async Task<bool> TryAcquireLockAsync(LockData lockData)
		{
			var db = GetDatabase();

			// Don't use IDatabase.LockTake() because we don't use the matching
			// LockRelease(). So, avoid any confusion. Besides, LockTake() just
			// calls this anyways.
			var wasLockAcquired = await db.StringSetAsync(lockData.LockKey, lockData.LockValue, lockTimeout, When.NotExists);

			if (wasLockAcquired)
			{
				// It's ok to use Set() instead of Add() because the lock in 
				// Redis will cause other clients to wait.
				acquiredLocks.Set(lockData.Key, lockData, absoluteExpiration: DateTime.UtcNow.Add(lockTimeout));
			}

			return wasLockAcquired;
		}
	}
}
