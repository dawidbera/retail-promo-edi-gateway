using Microsoft.Extensions.Caching.Memory;
using RetailEdiGateway.Application.Common.Interfaces;
using System;
using System.Threading.Tasks;

namespace RetailEdiGateway.Infrastructure.Services
{
 /// <summary>
 /// Implementation of ICacheService using Microsoft.Extensions.Caching.Memory.
 /// Used for caching slow-changing static metadata.
 /// </summary>
 public class InMemoryCacheService : ICacheService
 {
 private readonly IMemoryCache _memoryCache;

 /// <summary>
 /// Initializes a new instance of the <see cref="InMemoryCacheService"/> class.
 /// </summary>
 public InMemoryCacheService(IMemoryCache memoryCache)
 {
 _memoryCache = memoryCache;
 }

 /// <inheritdoc />
 public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
 {
 if (_memoryCache.TryGetValue(key, out T? cachedValue))
 {
 if (cachedValue != null)
 {
 return cachedValue;
 }
 }

 T value = await factory();

 var cacheOptions = new MemoryCacheEntryOptions
 {
 AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10)
 };

 _memoryCache.Set(key, value, cacheOptions);

 return value;
 }

 /// <inheritdoc />
 public void Remove(string key)
 {
 _memoryCache.Remove(key);
 }
 }
}
