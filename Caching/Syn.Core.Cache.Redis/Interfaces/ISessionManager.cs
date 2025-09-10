// This file is part of Syn.Core.Cache.Redis, a library for secure caching using Redis.
namespace Syn.Core.Cache.Redis.Interfaces;

public interface ISessionManager
{
    Task SetAsync<T>(string sessionId, string key, T value, TimeSpan? expiration = null);
    Task<T?> GetAsync<T>(string sessionId, string key);
    Task RemoveAsync(string sessionId, string key);
    //Task ClearSessionAsync(string sessionId);
}

