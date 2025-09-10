using Microsoft.EntityFrameworkCore;

namespace Sample.Web.Data
{
    /// <summary>
    /// Minimal DbContext. TenantFeatureFlag entity will be added via model customizer in the library.
    /// </summary>
    public class MyDbContext : DbContext
    {
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }
    }
}
