using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrafficStatistics.Data;

/// <summary>
/// Design-time factory used by EF Core migrations tooling (e.g. <c>dotnet ef migrations add</c>).
/// Creates a <see cref="TrafficDbContext"/> configured to use a local SQLite design database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TrafficDbContext>
{
    /// <summary>
    /// Creates a new <see cref="TrafficDbContext"/> instance for design-time services.
    /// </summary>
    /// <param name="args">Command-line arguments (unused).</param>
    /// <returns>A configured <see cref="TrafficDbContext"/>.</returns>
    public TrafficDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TrafficDbContext>();
        optionsBuilder.UseSqlite("Data Source=traffic_design.db");

        return new TrafficDbContext(optionsBuilder.Options);
    }
}
