namespace Syn.Core.MultiTenancy.Context;

/// <summary>
/// Marks a property as the TenantId for multi-tenancy filtering.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class TenantIdAttribute : Attribute { }
