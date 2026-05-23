using System;
using System.Threading.Tasks;

namespace RetailPromoEdiGateway.Application.Common.Interfaces
{
    /// <summary>
    /// Service interface for memory caching of static lists.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Retrieves an item from cache, or executes the factory to retrieve and store it.
        /// </summary>
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        void Remove(string key);
    }
}
