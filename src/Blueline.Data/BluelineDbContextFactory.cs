using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blueline.Data;

/// <summary>Used only by `dotnet ef` at design time; the running app configures its own connection string.</summary>
public class BluelineDbContextFactory : IDesignTimeDbContextFactory<BluelineDbContext>
{
    public BluelineDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BluelineDbContext>()
            .UseSqlite("Data Source=blueline.db")
            .Options;
        return new BluelineDbContext(options);
    }
}
