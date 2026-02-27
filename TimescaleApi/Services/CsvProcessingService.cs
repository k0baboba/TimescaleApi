using Microsoft.EntityFrameworkCore;
using TimescaleApi.Data;
using TimescaleApi.DTOs;
using TimescaleApi.Models;
using TimescaleApi.Validators;

namespace TimescaleApi.Services
{
    public class CsvProcessingService : ICsvProcessingService
    {
        private readonly AppDbContext _context;
        private readonly CsvValidator _validator;

        public CsvProcessingService(AppDbContext context, CsvValidator validator)
        {
            _context = context;
            _validator = validator;
        }

        public async Task<CsvProcessingResult> ProcessFileAsync(string fileName, Stream csvStream)
        {
            // 1. Валидация + парсинг (валидатор делает оба шага)
            var validationResult = _validator.Validate(csvStream);
            if (!validationResult.IsValid)
            {
                return new CsvProcessingResult
                {
                    IsSuccess = false,
                    ErrorMessage = string.Join("; ", validationResult.Errors)
                };
            }

            var records = validationResult.Records;

            // 2. Транзакция: удалить старые + добавить новые + рассчитать агрегаты
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Удалить старые записи по FileName
                await _context.Values
                    .Where(v => v.FileName == fileName)
                    .ExecuteDeleteAsync();

                await _context.Results
                    .Where(r => r.FileName == fileName)
                    .ExecuteDeleteAsync();

                // Добавить новые Values
                var valuesToAdd = records.Select(r => new Values
                {
                    FileName = fileName,
                    Date = DateTime.SpecifyKind(r.Date, DateTimeKind.Utc),
                    ExecutionTime = r.ExecutionTime,
                    Value = r.Value
                }).ToList();

                _context.Values.AddRange(valuesToAdd);
                await _context.SaveChangesAsync();

                // Рассчитать агрегаты в памяти
                var result = CalculateResult(fileName, records);

                _context.Results.Add(result);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new CsvProcessingResult
                {
                    IsSuccess = true,
                    Data = result
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new CsvProcessingResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Database error: {ex.Message}"
                };
            }
        }

        private static Result CalculateResult(string fileName, List<CsvRecord> records)
        {
            var dates = records.Select(r => r.Date).ToList();
            var values = records.Select(r => r.Value).OrderBy(v => v).ToList();

            var minDate = dates.Min();
            var maxDate = dates.Max();

            return new Result
            {
                FileName = fileName,
                DeltaTimeSeconds = (maxDate - minDate).TotalSeconds,
                MinDate = DateTime.SpecifyKind(minDate, DateTimeKind.Utc),
                AverageExecutionTime = records.Average(r => r.ExecutionTime),
                AverageValue = records.Average(r => r.Value),
                MedianValue = CalculateMedian(values),
                MaxValue = values.Last(),
                MinValue = values.First()
            };
        }

        private static double CalculateMedian(List<double> sorted)
        {
            int count = sorted.Count;
            if (count == 0) return 0;

            if (count % 2 == 1)
                return sorted[count / 2];
            else
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
    }
}