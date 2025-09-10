// This file is part of Syn.Core.Cache.Redis, a library for secure caching using Redis.
namespace Syn.Core.Cache.Redis.Interfaces;

/// <summary>
/// Defines the methods for a secure distributed cache that supports serialization, encryption, and compression.
/// </summary>
public interface ISecureCache
{
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task<T?> GetAsync<T>(string key);
    Task RemoveAsync(string key);
}
// This interface defines the methods for a secure distributed cache that supports serialization, encryption, and compression.