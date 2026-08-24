using Microsoft.EntityFrameworkCore;
using TrafficStatistics.Core.Models;

namespace TrafficStatistics.Data;

/// <summary>
/// Entity Framework Core database context for the traffic statistics SQLite database.
/// </summary>
public class TrafficDbContext : DbContext
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of <see cref="TrafficDbContext"/> with a custom connection string.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public TrafficDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TrafficDbContext"/> with the default database path.
    /// </summary>
    public TrafficDbContext()
        : this("Data Source=traffic.db")
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TrafficDbContext"/> with externally supplied options.
    /// </summary>
    /// <param name="options">The options to configure the context.</param>
    public TrafficDbContext(DbContextOptions<TrafficDbContext> options)
        : base(options)
    {
        _connectionString = string.Empty;
    }

    /// <summary>
    /// Gets or sets the set of tracked processes.
    /// </summary>
    public DbSet<ProcessEntity> Processes => Set<ProcessEntity>();

    /// <summary>
    /// Gets or sets the set of per-minute traffic records.
    /// </summary>
    public DbSet<TrafficRecord> TrafficRecords => Set<TrafficRecord>();

    /// <summary>
    /// Gets or sets the set of daily aggregated summaries.
    /// </summary>
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite(_connectionString);
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ProcessEntity configuration
        modelBuilder.Entity<ProcessEntity>(entity =>
        {
            entity.ToTable("Processes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.Path }).IsUnique();
        });

        // TrafficRecord configuration
        modelBuilder.Entity<TrafficRecord>(entity =>
        {
            entity.ToTable("TrafficRecords");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.ProcessId, e.Timestamp }).IsUnique();
        });

        // DailySummary configuration
        modelBuilder.Entity<DailySummary>(entity =>
        {
            entity.ToTable("DailySummaries");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ProcessId, e.Date }).IsUnique();
        });
    }
}
