using Microsoft.Extensions.Logging;

using Syn.Core.Cache.Redis.Interfaces;

namespace Syn.Core.Cache.Redis.Sessions;

internal class SessionManager : ISessionManager
{
    private readonly ISecureCache _secureCache;
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(ISecureCache secureCache, ILogger<SessionManager> logger)
    {
        _secureCache = secureCache;
        _logger = logger;
    }

    private string BuildKey(string sessionId, string key) => $"session:{sessionId}:{key}";

    public async Task SetAsync<T>(string sessionId, string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            await _secureCache.SetAsync(BuildKey(sessionId, key), value, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set session for key {Key}", key);
            throw;
        }
    }

    public async Task<T?> GetAsync<T>(string sessionId, string key)
    {
        try
        {
            var result = await _secureCache.GetAsync<T>(BuildKey(sessionId, key));
            return result is T typed ? typed : default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session for key {Key}", key);
            return default;
        }
    }

    public async Task RemoveAsync(string sessionId, string key)
    {
        try
        {
            await _secureCache.RemoveAsync(BuildKey(sessionId, key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove session for key {Key}", key);
        }
    }

    public async Task ClearSessionAsync(string sessionId)
    {
        // لو بنستخدم Redis، ممكن نعمل Scan و Remove لكل المفاتيح اللي تبدأ بـ sessionId
        // لكن ده محتاج Redis client مباشر مش IDistributedCache
        throw new NotImplementedException("Requires direct Redis access for key scanning.");
    }


}