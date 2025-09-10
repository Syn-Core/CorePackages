using Syn.Core.MultiTenancy.Metadata;

namespace Syn.Core.MultiTenancy.Context
{
    /// <summary>
    /// Default implementation of <see cref="ITenantContext"/> that supports both single-tenant
    /// and multi-tenant scenarios.
    /// </summary>
    /// <remarks>
    /// This context stores a collection of tenants available to the current user and
    /// an active tenant that can be used for operations requiring a single tenant scope.
    /// <para>
    /// If only one tenant is provided, it will automatically be set as the <see cref="ActiveTenant"/>.
    /// </para>
    /// </remarks>
    public class MultiTenantContext : ITenantContext
    {
        /// <summary>
        /// Gets the list of tenants available to the current user.
        /// </summary>
        public IReadOnlyList<TenantInfo> Tenants { get; }

        /// <summary>
        /// Gets or sets the currently active tenant.
        /// This is useful for operations that require a single tenant context.
        /// </summary>
        public TenantInfo? ActiveTenant { get; set; }

        /// <summary>
        /// Gets the default tenant property name (e.g., "TenantId") used for filtering or identification.
        /// </summary>
        public string TenantPropertyName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiTenantContext"/> class.
        /// </summary>
        /// <param name="tenants">
        /// The collection of tenants available to the current user.
        /// </param>
        /// <param name="tenantPropertyName">
        /// The default tenant property name (e.g., "TenantId").
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="tenants"/> or <paramref name="tenantPropertyName"/> is null.
        /// </exception>
        public MultiTenantContext(IEnumerable<TenantInfo> tenants, string tenantPropertyName)
        {
            if (tenants == null) throw new ArgumentNullException(nameof(tenants));
            if (tenantPropertyName == null) throw new ArgumentNullException(nameof(tenantPropertyName));

            Tenants = tenants.ToList();
            TenantPropertyName = tenantPropertyName;
            ActiveTenant = Tenants.FirstOrDefault();
        }


        /// <summary>
        /// Sets the active tenant by its identifier, if it exists in the available tenants list.
        /// </summary>
        /// <param name="tenantId">The tenant identifier to set as active.</param>
        /// <returns>
        /// <c>true</c> if the active tenant was successfully set; otherwise, <c>false</c>.
        /// </returns>
        public bool SetActiveTenant(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return false;

            var tenant = Tenants.FirstOrDefault(t =>
                t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));

            if (tenant != null)
            {
                ActiveTenant = tenant;
                return true;
            }

            return false;
        }

    }

}