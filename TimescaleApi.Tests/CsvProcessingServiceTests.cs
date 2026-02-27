using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TimescaleApi.Data;
using TimescaleApi.Services;
using TimescaleApi.Validators;

namespace TimescaleApi.Tests;

public class CsvProcessingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly CsvProcessingService _service;

    public CsvProcessingServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        _service = new CsvProcessingService(_context, new CsvValidator());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static Stream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public async Task ProcessFileAsync_ValidCsv_SavesValuesAndResult()
    {
        var csv = "Date;ExecutionTime;Value\n" +
                  "2023-06-15T10-30-00.0000Z;5.5;100.25\n" +
                  "2023-06-16T11-00-00.0000Z;3.2;200.50";
        using var stream = CreateStream(csv);

        var result = await _service.ProcessFileAsync("test.csv", stream);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await _context.Values.CountAsync());
        Assert.Equal(1, await _context.Results.CountAsync());
    }

    [Fact]
    public async Task ProcessFileAsync_InvalidCsv_ReturnsErrorAndNoData()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;-1;100.25";
        using var stream = CreateStream(csv);

        var result = await _service.ProcessFileAsync("test.csv", stream);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, await _context.Values.CountAsync());
        Assert.Equal(0, await _context.Results.CountAsync());
    }

    [Fact]
    public async Task ProcessFileAsync_ExistingFile_OverwritesData()
    {
        var csv1 = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;5.5;100.25";
        using (var stream1 = CreateStream(csv1))
        {
            await _service.ProcessFileAsync("test.csv", stream1);
        }

        var csv2 = "Date;ExecutionTime;Value\n" +
                   "2023-07-01T12-00-00.0000Z;2.0;50.0\n" +
                   "2023-07-02T13-00-00.0000Z;3.0;75.0";
        using (var stream2 = CreateStream(csv2))
        {
            await _service.ProcessFileAsync("test.csv", stream2);
        }

        Assert.Equal(2, await _context.Values.CountAsync());
        Assert.Equal(1, await _context.Results.CountAsync());

        var savedResult = await _context.Results.SingleAsync();
        Assert.Equal("test.csv", savedResult.FileName);
        Assert.Equal(62.5, savedResult.AverageValue);
    }

    [Fact]
    public async Task ProcessFileAsync_CalculatesCorrectAggregates()
    {
        var csv = "Date;ExecutionTime;Value\n" +
                  "2023-01-01T10-00-00.0000Z;2;10\n" +
                  "2023-01-01T11-00-00.0000Z;4;20\n" +
                  "2023-01-01T12-00-00.0000Z;6;30";
        using var stream = CreateStream(csv);

        var result = await _service.ProcessFileAsync("agg.csv", stream);

        Assert.True(result.IsSuccess);
        var r = result.Data!;
        Assert.Equal(7200, r.DeltaTimeSeconds);
        Assert.Equal(new DateTime(2023, 1, 1, 10, 0, 0, DateTimeKind.Utc), r.MinDate);
        Assert.Equal(4.0, r.AverageExecutionTime);
        Assert.Equal(20.0, r.AverageValue);
        Assert.Equal(20.0, r.MedianValue);
        Assert.Equal(30.0, r.MaxValue);
        Assert.Equal(10.0, r.MinValue);
    }

    [Fact]
    public async Task ProcessFileAsync_EvenRowCount_CalculatesCorrectMedian()
    {
        var csv = "Date;ExecutionTime;Value\n" +
                  "2023-01-01T10-00-00.0000Z;1;10\n" +
                  "2023-01-01T11-00-00.0000Z;1;20\n" +
                  "2023-01-01T12-00-00.0000Z;1;30\n" +
                  "2023-01-01T13-00-00.0000Z;1;40";
        using var stream = CreateStream(csv);

        var result = await _service.ProcessFileAsync("median.csv", stream);

        Assert.True(result.IsSuccess);
        Assert.Equal(25.0, result.Data!.MedianValue);
    }

    [Fact]
    public async Task ProcessFileAsync_SingleRow_DeltaTimeIsZero()
    {
        var csv = "Date;ExecutionTime;Value\n2023-01-01T10-00-00.0000Z;5;42.5";
        using var stream = CreateStream(csv);

        var result = await _service.ProcessFileAsync("single.csv", stream);

        Assert.True(result.IsSuccess);
        var r = result.Data!;
        Assert.Equal(0, r.DeltaTimeSeconds);
        Assert.Equal(5.0, r.AverageExecutionTime);
        Assert.Equal(42.5, r.AverageValue);
        Assert.Equal(42.5, r.MedianValue);
        Assert.Equal(42.5, r.MaxValue);
        Assert.Equal(42.5, r.MinValue);
    }

    [Fact]
    public async Task ProcessFileAsync_DifferentFileNames_StoresSeparately()
    {
        var csv1 = "Date;ExecutionTime;Value\n2023-01-01T10-00-00.0000Z;1;10";
        var csv2 = "Date;ExecutionTime;Value\n2023-02-01T10-00-00.0000Z;2;20";

        using (var stream1 = CreateStream(csv1))
        {
            await _service.ProcessFileAsync("file1.csv", stream1);
        }
        using (var stream2 = CreateStream(csv2))
        {
            await _service.ProcessFileAsync("file2.csv", stream2);
        }

        Assert.Equal(2, await _context.Values.CountAsync());
        Assert.Equal(2, await _context.Results.CountAsync());
    }
}
