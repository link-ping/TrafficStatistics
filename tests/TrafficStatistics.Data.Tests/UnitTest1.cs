using TrafficStatistics.Core.Models;
using TrafficStatistics.Data;
using TrafficStatistics.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace TrafficStatistics.Data.Tests;

public class TrafficRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TrafficDbContext> _options;

    public TrafficRepositoryTests()
    {
        // Setup SQLite in-memory database connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TrafficDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Ensure database schema is created
        using var context = new TrafficDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private TrafficDbContext GetContext()
    {
        return new TrafficDbContext(_options);
    }

    [Fact]
    public async Task GetOrCreateProcessAsync_ShouldCreateProcess_WhenNotExists()
    {
        using var context = GetContext();
        var repository = new TrafficRepository(context);

        var result = await repository.GetOrCreateProcessAsync("chrome.exe", @"C:\Program Files\Chrome\chrome.exe");

        Assert.NotNull(result);
        Assert.Equal("chrome.exe", result.Name);
        Assert.Equal(@"C:\Program Files\Chrome\chrome.exe", result.Path);
        Assert.True(result.Id > 0);

        // Verify database persistence
        using var checkContext = GetContext();
        var dbCount = await checkContext.Processes.CountAsync();
        Assert.Equal(1, dbCount);
    }

    [Fact]
    public async Task GetOrCreateProcessAsync_ShouldReturnExistingProcess_WhenExists()
    {
        using var context = GetContext();
        var repository = new TrafficRepository(context);

        var initial = await repository.GetOrCreateProcessAsync("chrome.exe", @"C:\Program Files\Chrome\chrome.exe");
        var second = await repository.GetOrCreateProcessAsync("chrome.exe", @"C:\Program Files\Chrome\chrome.exe");

        Assert.Equal(initial.Id, second.Id);
        
        using var checkContext = GetContext();
        var dbCount = await checkContext.Processes.CountAsync();
        Assert.Equal(1, dbCount);
    }

    [Fact]
    public async Task SaveTrafficRecordsAsync_ShouldBatchInsertOrReplaceRecords()
    {
        using var context = GetContext();
        var repository = new TrafficRepository(context);

        // Create process first
        var proc = await repository.GetOrCreateProcessAsync("chrome.exe", null);

        var records = new List<(int processId, long timestamp, long bytesSent, long bytesRecv)>
        {
            (proc.Id, 1781604900, 1000, 2000),
            (proc.Id, 1781604960, 1500, 3000)
        };

        await repository.SaveTrafficRecordsAsync(records);

        // Verify they exist
        using var checkContext = GetContext();
        var saved = await checkContext.TrafficRecords.OrderBy(r => r.Timestamp).ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.Equal(1000, saved[0].BytesSent);
        Assert.Equal(2000, saved[0].BytesRecv);
        Assert.Equal(1500, saved[1].BytesSent);
        Assert.Equal(3000, saved[1].BytesRecv);

        // Test replacement (upsert)
        var updateRecords = new List<(int processId, long timestamp, long bytesSent, long bytesRecv)>
        {
            (proc.Id, 1781604900, 5000, 8000) // update first record
        };

        await repository.SaveTrafficRecordsAsync(updateRecords);

        var updated = await checkContext.TrafficRecords.OrderBy(r => r.Timestamp).ToListAsync();
        Assert.Equal(2, updated.Count); // still 2 records
        Assert.Equal(5000, updated[0].BytesSent); // updated
        Assert.Equal(8000, updated[0].BytesRecv); // updated
    }

    [Fact]
    public async Task PurgeOldData_ShouldRemoveStaleRecords()
    {
        using var context = GetContext();
        var repository = new TrafficRepository(context);

        var proc = await repository.GetOrCreateProcessAsync("chrome.exe", null);

        long nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long oldTs = DateTimeOffset.UtcNow.AddDays(-10).ToUnixTimeSeconds();

        var records = new List<(int processId, long timestamp, long bytesSent, long bytesRecv)>
        {
            (proc.Id, nowTs, 100, 200),
            (proc.Id, oldTs, 500, 1000) // should be purged
        };

        await repository.SaveTrafficRecordsAsync(records);
        await repository.PurgeOldDataAsync(7, 30); // minute retention = 7 days

        using var checkContext = GetContext();
        var remaining = await checkContext.TrafficRecords.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(nowTs, remaining[0].Timestamp);
    }
}
