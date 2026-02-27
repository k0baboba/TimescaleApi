using TimescaleApi.Models;

namespace TimescaleApi.Services
{
    public interface ICsvProcessingService
    {
        Task<CsvProcessingResult> ProcessFileAsync(string fileName, Stream csvStream);
    }

    public class CsvProcessingResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public Result? Data { get; set; }
    }
}
