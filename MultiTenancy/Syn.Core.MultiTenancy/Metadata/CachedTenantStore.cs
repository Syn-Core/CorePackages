using Microsoft.Extensions.Caching.Memory;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Syn.Core.MultiTenancy.Metadata
{
    /// <summary>
    /// Caches results from an underlying ITenantStore using IMemoryCache.
    /// Cache keys are "tenant:{tenantId}:{includeInactive}" for single tenant
    /// and "tenants:all:{includeInactive}" for all tenants.
    /// </summary>
    public class CachedTenantStore : ITenantStore, ITenantCache
    {
        private readonly ITenantStore _inner;
        private readonly IMemoryCache _cache;
        private readonly MemoryCacheEntryOptions _options;

        public CachedTenantStore(ITenantStore inner, IMemoryCache cache, TimeSpan? ttl = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromMinutes(5)
            };
        }

        private static string Key(string tenantId, bool includeInactive)
            => $"tenant:{tenantId}:{includeInactive}";

        private static string AllKey(bool includeInactive)
            => $"tenants:all:{includeInactive}";


        public TenantInfo? Get(string tenantId, bool includeInactive = false)
        {
            if (_cache.TryGetValue(Key(tenantId, includeInactive), out TenantInfo? info))
                return info;

            var loaded = _inner.Get(tenantId, includeInactive);
            if (loaded != null)
                _cache.Set(Key(tenantId, includeInactive), loaded, _options);

            return loaded;
        }

        public IReadOnlyList<TenantInfo> GetAll(bool includeInactive = false)
        {
            if (_cache.TryGetValue(AllKey(includeInactive), out IReadOnlyList<TenantInfo>? list))
                return list;

            var loaded = _inner.GetAll(includeInactive);
            _cache.Set(AllKey(includeInactive), loaded, _options);

            return loaded;
        }

        public void AddOrUpdate(TenantInfo tenant)
        {
            _inner.AddOrUpdate(tenant);

            // تحديث الكاش لكل الحالات (Active/Inactive)
            _cache.Set(Key(tenant.TenantId, true), tenant, _options);
            if (tenant.IsActive)
                _cache.Set(Key(tenant.TenantId, false), tenant, _options);
            else
                _cache.Remove(Key(tenant.TenantId, false));

            // مسح كاش القائمة الكاملة
            _cache.Remove(AllKey(true));
            _cache.Remove(AllKey(false));
        }

        public bool Delete(string tenantId)
        {
            var result = _inner.Delete(tenantId);

            _cache.Remove(Key(tenantId, true));
            _cache.Remove(Key(tenantId, false));
            _cache.Remove(AllKey(true));
            _cache.Remove(AllKey(false));

            return result;
        }

        public async Task<TenantInfo?> GetAsync(string tenantId, bool includeInactive = false, CancellationToken ct = default)
        {
            if (_cache.TryGetValue(Key(tenantId, includeInactive), out TenantInfo? info))
                return info;

            var loaded = await _inner.GetAsync(tenantId, includeInactive, ct);
            if (loaded != null)
                _cache.Set(Key(tenantId, includeInactive), loaded, _options);

            return loaded;
        }

        public async Task<IReadOnlyList<TenantInfo>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            if (_cache.TryGetValue(AllKey(includeInactive), out IReadOnlyList<TenantInfo>? list))
                return list;

            var loaded = await _inner.GetAllAsync(includeInactive, ct);
            _cache.Set(AllKey(includeInactive), loaded, _options);

            return loaded;
        }

        public async Task AddOrUpdateAsync(TenantInfo tenant, CancellationToken ct = default)
        {
            await _inner.AddOrUpdateAsync(tenant, ct);

            // تحديث الكاش لكل الحالات (Active/Inactive)
            _cache.Set(Key(tenant.TenantId, true), tenant, _options);
            if (tenant.IsActive)
                _cache.Set(Key(tenant.TenantId, false), tenant, _options);
            else
                _cache.Remove(Key(tenant.TenantId, false));

            // مسح كاش القائمة الكاملة
            _cache.Remove(AllKey(true));
            _cache.Remove(AllKey(false));
        }

        public async Task<bool> DeleteAsync(string tenantId, CancellationToken ct = default)
        {
            var result = await _inner.DeleteAsync(tenantId, ct);

            _cache.Remove(Key(tenantId, true));
            _cache.Remove(Key(tenantId, false));
            _cache.Remove(AllKey(true));
            _cache.Remove(AllKey(false));

            return result;
        }

        Task<TenantInfo?> ITenantCache.GetAsync(string tenantId, Func<Task<TenantInfo?>> factory, CancellationToken ct)
            => GetAsync(tenantId, false, ct);

        Task ITenantCache.InvalidateAsync(string tenantId, CancellationToken ct)
        {
            _cache.Remove(Key(tenantId, true));
            _cache.Remove(Key(tenantId, false));
            return Task.CompletedTask;
        }

        Task ITenantCache.InvalidateAllAsync(CancellationToken ct)
        {
            // مفيش Global Clear في IMemoryCache، الحل إنك تمسح المفاتيح اللي بتعرفها أو تعيد إنشاء الكاش
            return Task.CompletedTask;
        }

        
    }
}