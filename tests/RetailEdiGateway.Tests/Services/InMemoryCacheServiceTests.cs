using Microsoft.Extensions.Caching.Memory;
using RetailEdiGateway.Infrastructure.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace RetailEdiGateway.Tests.Services
{
    /// <summary>
    /// Unit tests for the <see cref="InMemoryCacheService"/> class.
    /// Verifies that items are correctly cached and retrieved, and that factory is called only when necessary.
    /// </summary>
    public class InMemoryCacheServiceTests
    {
        private readonly IMemoryCache _memoryCache;
        private readonly InMemoryCacheService _cacheService;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryCacheServiceTests"/> class.
        /// </summary>
        public InMemoryCacheServiceTests()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _cacheService = new InMemoryCacheService(_memoryCache);
        }

        /// <summary>
        /// Verifies that the first call invokes the factory and subsequent calls return the cached value.
        /// </summary>
        [Fact]
        public async Task GetOrCreateAsync_FirstCall_InvokesFactoryAndCaches()
        {
            // Arrange
            string key = "test_key";
            int factoryCalls = 0;
            Func<Task<string>> factory = () =>
            {
                factoryCalls++;
                return Task.FromResult("Value1");
            };

            // Act
            var result1 = await _cacheService.GetOrCreateAsync(key, factory);
            var result2 = await _cacheService.GetOrCreateAsync(key, factory);

            // Assert
            Assert.Equal("Value1", result1);
            Assert.Equal("Value1", result2);
            Assert.Equal(1, factoryCalls);
        }

        /// <summary>
        /// Verifies that <see cref="InMemoryCacheService.Remove"/> correctly invalidates the cache.
        /// </summary>
        [Fact]
        public async Task Remove_ExistingKey_InvalidatesCache()
        {
            // Arrange
            string key = "remove_key";
            int factoryCalls = 0;
            Func<Task<string>> factory = () =>
            {
                factoryCalls++;
                return Task.FromResult("Value1");
            };

            await _cacheService.GetOrCreateAsync(key, factory);

            // Act
            _cacheService.Remove(key);
            var result = await _cacheService.GetOrCreateAsync(key, factory);

            // Assert
            Assert.Equal("Value1", result);
            Assert.Equal(2, factoryCalls);
        }
    }
}
