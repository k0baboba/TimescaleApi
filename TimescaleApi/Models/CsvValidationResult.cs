using TimescaleApi.DTOs;

namespace TimescaleApi.Models
{
    public class CsvValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new();
        public List<CsvRecord> Records { get; } = new();
    }
}
