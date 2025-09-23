using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBatteryShop.DataAccess;

/// <summary>
/// Dedicated DbContext for persisting ASP.NET Core data protection keys.
/// Keeping it separate from the main application context avoids schema
/// verification side-effects when the key repository initialises.
/// </summary>
public sealed class DataProtectionKeyContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeyContext(DbContextOptions<DataProtectionKeyContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}
