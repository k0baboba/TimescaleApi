using System.Globalization;
using System.Linq;
using TimescaleApi.DTOs;
using TimescaleApi.Models;

namespace TimescaleApi.Validators
{
    public class CsvValidator
    {
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

            if (csvStream is null || !csvStream.CanRead)
            {
                result.Errors.Add("CSV stream is null or unreadable.");
                return result;
            }

            using var reader = new StreamReader(csvStream, leaveOpen: true);

            var nowUtc = DateTime.UtcNow;
            var headerProcessed = false;
            var lineNumber = 0;

            while (true)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                lineNumber++;

                if (!headerProcessed)
                {
                    headerProcessed = true;

                    if (IsHeader(line))
                    {
                        continue;
                    }
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    result.Errors.Add($"Line {lineNumber}: empty line.");
                    return result;
                }

                if (!TryParseRow(line, lineNumber, nowUtc, result, out var record))
                {
                    return result;
                }

                result.Records.Add(record!);

                if (result.Records.Count > 10_000)
                {
                    result.Errors.Add("CSV contains more than 10,000 data rows.");
                    return result;
                }
            }

            if (result.Records.Count < 1)
            {
                result.Errors.Add("CSV contains no data rows.");
            }

            return result;
        }

        private static bool IsHeader(string line)
        {
            return string.Equals(line.Trim(), "Date;ExecutionTime;Value", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseRow(
            string line,
            int lineNumber,
            DateTime nowUtc,
            CsvValidationResult result,
            out CsvRecord? record)
        {
            record = null;

            var parts = line.Split(';', StringSplitOptions.None);
            if (parts.Length != 3)
            {
                result.Errors.Add($"Line {lineNumber}: expected 3 fields separated by ';'.");
                return false;
            }

            if (parts.Any(string.IsNullOrWhiteSpace))
            {
                result.Errors.Add($"Line {lineNumber}: all fields must be present.");
                return false;
            }

            if (!DateTime.TryParseExact(
                    parts[0].Trim(),
                    DateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var date))
            {
                result.Errors.Add($"Line {lineNumber}: invalid Date format.");
                return false;
            }

            if (date < MinDate || date > nowUtc)
            {
                result.Errors.Add($"Line {lineNumber}: Date must be between 2000-01-01 and now (UTC).");
                return false;
            }

            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var executionTime) || executionTime < 0)
            {
                result.Errors.Add($"Line {lineNumber}: ExecutionTime must be a number >= 0.");
                return false;
            }

            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0)
            {
                result.Errors.Add($"Line {lineNumber}: Value must be a number >= 0.");
                return false;
            }

            record = new CsvRecord
            {
                Date = date,
                ExecutionTime = executionTime,
                Value = value
            };

            return true;
        }
    }
}
