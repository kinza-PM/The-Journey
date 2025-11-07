using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TheJourney.Api.Infrastructure.Database;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var pgHost = Environment.GetEnvironmentVariable("PG_HOST") ?? "localhost";
        var pgUser = Environment.GetEnvironmentVariable("PG_USER") ?? "postgres";
        var pgPassword = Environment.GetEnvironmentVariable("PG_PASSWORD") ?? "";
        var pgDb = Environment.GetEnvironmentVariable("PG_DB") ?? "thejourney";
        
        var connectionString = $"Host={pgHost};Username={pgUser};Password={pgPassword};Database={pgDb};SSL Mode=Require;Trust Server Certificate=true";
        
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        
        return new AppDbContext(optionsBuilder.Options);
    }
}
