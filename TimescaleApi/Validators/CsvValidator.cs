using System.Globalization;
using TimescaleApi.DTOs;
using TimescaleApi.Models;

namespace TimescaleApi.Validators
{
    public class CsvValidator
    {
        private const int MaxRowCount = 10_000;

        public static readonly DateTime MinDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly string[] DateFormats =
            [
            "yyyy-MM-ddTHH-mm-ss.ffffZ",
            "yyyy-MM-ddTHH:mm:ss.ffffZ",
            "yyyy-MM-ddTHH-mm-ssZ",
            "yyyy-MM-ddTHH:mm:ssZ"
            ];

        public CsvValidationResult Validate(Stream csvStream)
        {
            var result = new CsvValidationResult();
            using var reader = new StreamReader(csvStream);

            var header = reader.ReadLine();
            if (header is null)
            {
                result.Errors.Add("File is empty.");
                return result;
            }

            int lineNumber = 1;
            int rowCount = 0;
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                rowCount++;

                if (rowCount > MaxRowCount)
                {
                    result.Records.Clear();
                    result.Errors.Add($"File contains more than {MaxRowCount} rows.");
                    return result;
                }

                var parts = line.Split(';');
                if (parts.Length != 3)
                {
                    result.Errors.Add($"Line {lineNumber}: expected 3 values, got {parts.Length}.");
                    continue;
                }

                if (!DateTime.TryParseExact(parts[0].Trim(), DateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal, out var date))
                {
                    result.Errors.Add($"Line {lineNumber}: invalid date format.");
                    continue;
                }

                if (date < MinDate)
                {
                    result.Errors.Add($"Line {lineNumber}: date cannot be earlier than 2000-01-01.");
                    continue;
                }

                if (date > DateTime.UtcNow)
                {
                    result.Errors.Add($"Line {lineNumber}: date cannot be in the future.");
                    continue;
                }

                var execTimeStr = parts[1].Trim().Replace(',', '.');
                if (!double.TryParse(execTimeStr, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var executionTime))
                {
                    result.Errors.Add($"Line {lineNumber}: invalid execution time format.");
                    continue;
                }

                if (executionTime < 0)
                {
                    result.Errors.Add($"Line {lineNumber}: execution time cannot be negative.");
                    continue;
                }

                var valueStr = parts[2].Trim().Replace(',', '.');
                if (!double.TryParse(valueStr, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var value))
                {
                    result.Errors.Add($"Line {lineNumber}: invalid value format.");
                    continue;
                }

                if (value < 0)
                {
                    result.Errors.Add($"Line {lineNumber}: value cannot be negative.");
                    continue;
                }

                result.Records.Add(new CsvRecord
                {
                    Date = date,
                    ExecutionTime = executionTime,
                    Value = value
                });
            }

            if (rowCount == 0)
            {
                result.Errors.Add("File contains no data rows.");
            }

            return result;
        }
    }
}
