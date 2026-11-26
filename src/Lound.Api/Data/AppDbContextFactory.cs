using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lound.Api.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
            "Server=(localdb)\\MSSQLLocalDB;Database=LoundDev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        return new AppDbContext(options);
    }
}
