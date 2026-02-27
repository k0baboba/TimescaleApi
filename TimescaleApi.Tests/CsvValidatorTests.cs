using System.Text;
using TimescaleApi.Validators;

namespace TimescaleApi.Tests;

public class CsvValidatorTests
{
    private readonly CsvValidator _validator = new();

    private static Stream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public void Validate_ValidSingleRow_ReturnsValid()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;5.5;100.25";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.True(result.IsValid);
        Assert.Single(result.Records);
        Assert.Equal(5.5, result.Records[0].ExecutionTime);
        Assert.Equal(100.25, result.Records[0].Value);
    }

    [Fact]
    public void Validate_MultipleValidRows_ParsesAll()
    {
        var csv = "Date;ExecutionTime;Value\n" +
                  "2023-06-15T10-30-00.0000Z;5.5;100.25\n" +
                  "2023-06-16T11-00-00.0000Z;3.2;200.50";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Records.Count);
    }

    [Fact]
    public void Validate_EmptyFile_ReturnsError()
    {
        using var stream = CreateStream("");

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Fact]
    public void Validate_OnlyHeader_ReturnsError()
    {
        var csv = "Date;ExecutionTime;Value\n";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no data"));
    }

    [Fact]
    public void Validate_DateBeforeMinDate_ReturnsError()
    {
        var csv = "Date;ExecutionTime;Value\n1999-12-31T23-59-59.0000Z;1.0;10.0";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("earlier than 2000"));
    }

    [Fact]
    public void Validate_FutureDate_ReturnsError()
    {
        var csv = "Date;ExecutionTime;Value\n2099-01-01T00-00-00.0000Z;1.0;10.0";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("future"));
    }

    [Fact]
    public void Validate_NegativeExecutionTime_ReturnsError()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;-1.0;10.0";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("execution time") && e.Contains("negative"));
    }

    [Fact]
    public void Validate_NegativeValue_ReturnsError()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;1.0;-5.0";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("value") && e.Contains("negative"));
    }

    [Fact]
    public void Validate_MissingColumn_ReturnsError()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;1.0";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("expected 3"));
    }

    [Fact]
    public void Validate_InvalidDateFormat_ReturnsError()
    {
        var csv = "Date;ExecutionTime;Value\nnot-a-date;1.0;10.0";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid date"));
    }

    [Fact]
    public void Validate_InvalidExecutionTimeFormat_ReturnsError()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;abc;10.0";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid execution time"));
    }

    [Fact]
    public void Validate_InvalidValueFormat_ReturnsError()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;1.0;xyz";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid value"));
    }

    [Fact]
    public void Validate_ColonFormatDate_ReturnsValid()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10:30:00.0000Z;5.5;100.25";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.True(result.IsValid);
        Assert.Single(result.Records);
    }

    [Fact]
    public void Validate_ZeroExecutionTimeAndValue_ReturnsValid()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;0;0";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.True(result.IsValid);
        Assert.Single(result.Records);
        Assert.Equal(0, result.Records[0].ExecutionTime);
        Assert.Equal(0, result.Records[0].Value);
    }

    [Fact]
    public void Validate_MoreThan10000Rows_ReturnsError()
    {
        var sb = new StringBuilder("Date;ExecutionTime;Value\n");
        for (int i = 0; i < 10_001; i++)
        {
            sb.AppendLine("2023-06-15T10-30-00.0000Z;1.0;10.0");
        }
        using var stream = CreateStream(sb.ToString());

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("10000"));
        Assert.Empty(result.Records);
    }

    [Fact]
    public void Validate_Exactly10000Rows_ReturnsValid()
    {
        var sb = new StringBuilder("Date;ExecutionTime;Value\n");
        for (int i = 0; i < 10_000; i++)
        {
            sb.AppendLine("2023-06-15T10-30-00.0000Z;1.0;10.0");
        }
        using var stream = CreateStream(sb.ToString());

        var result = _validator.Validate(stream);

        Assert.True(result.IsValid);
        Assert.Equal(10_000, result.Records.Count);
    }

    [Fact]
    public void Validate_MultipleErrors_CollectsAll()
    {
        var csv = "Date;ExecutionTime;Value\n" +
                  "not-a-date;1.0;10.0\n" +
                  "2023-06-15T10-30-00.0000Z;-1;10.0\n" +
                  "2023-06-15T10-30-00.0000Z;1.0;-5.0";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Validate_CommaDecimalSeparator_ParsesCorrectly()
    {
        var csv = "Date;ExecutionTime;Value\n" +
                  "2024-01-15T10-30-00.0000Z;1,50;100,25\n" +
                  "2024-01-15T10-31-00.0000Z;2,30;200,50\n" +
                  "2024-01-15T10-32-00.0000Z;0,80;150,75";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Records.Count);
        Assert.Equal(1.5, result.Records[0].ExecutionTime);
        Assert.Equal(100.25, result.Records[0].Value);
        Assert.Equal(2.3, result.Records[1].ExecutionTime);
        Assert.Equal(200.5, result.Records[1].Value);
        Assert.Equal(0.8, result.Records[2].ExecutionTime);
        Assert.Equal(150.75, result.Records[2].Value);
    }

    [Fact]
    public void Validate_DotDecimalSeparator_StillWorks()
    {
        var csv = "Date;ExecutionTime;Value\n2023-06-15T10-30-00.0000Z;1.50;100.25";
        using var stream = CreateStream(csv);

        var result = _validator.Validate(stream);

        Assert.True(result.IsValid);
        Assert.Equal(1.5, result.Records[0].ExecutionTime);
        Assert.Equal(100.25, result.Records[0].Value);
    }
}
