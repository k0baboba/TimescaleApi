using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimescaleApi.Data;
using TimescaleApi.DTOs;
using TimescaleApi.Services;

namespace TimescaleApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ValuesController : ControllerBase
    {
        private readonly ICsvProcessingService _csvService;
        private readonly AppDbContext _context;

        public ValuesController(ICsvProcessingService csvService, AppDbContext context)
        {
            _csvService = csvService;
            _context = context;
        }

        /// <summary>
        /// Метод 1: Загрузка CSV-файла, парсинг, валидация и сохранение в БД.
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file is null || file.Length == 0)
                return BadRequest("File is empty or not provided.");

            using var stream = file.OpenReadStream();
            var result = await _csvService.ProcessFileAsync(file.FileName, stream);

            if (!result.IsSuccess)
                return BadRequest(result.ErrorMessage);

            return Ok(result.Data);
        }

        /// <summary>
        /// Метод 2: Получение Results с фильтрами.
        /// </summary>
        [HttpGet("results")]
        public async Task<IActionResult> GetResults([FromQuery] ResultFilterDto filter)
        {
            var query = _context.Results.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.FileName))
                query = query.Where(r => r.FileName == filter.FileName);

            if (filter.MinDateFrom.HasValue)
                query = query.Where(r => r.MinDate >= filter.MinDateFrom.Value.ToUniversalTime());

            if (filter.MinDateTo.HasValue)
                query = query.Where(r => r.MinDate <= filter.MinDateTo.Value.ToUniversalTime());

            if (filter.AvgValueFrom.HasValue)
                query = query.Where(r => r.AverageValue >= filter.AvgValueFrom.Value);

            if (filter.AvgValueTo.HasValue)
                query = query.Where(r => r.AverageValue <= filter.AvgValueTo.Value);

            if (filter.AvgExecutionTimeFrom.HasValue)
                query = query.Where(r => r.AverageExecutionTime >= filter.AvgExecutionTimeFrom.Value);

            if (filter.AvgExecutionTimeTo.HasValue)
                query = query.Where(r => r.AverageExecutionTime <= filter.AvgExecutionTimeTo.Value);

            var results = await query.AsNoTracking().ToListAsync();
            return Ok(results);
        }

        /// <summary>
        /// Метод 3: Последние 10 значений по имени файла, отсортированных по Date.
        /// </summary>
        [HttpGet("values/{fileName}")]
        public async Task<IActionResult> GetLastValues(string fileName)
        {
            var values = await _context.Values
                .Where(v => v.FileName == fileName)
                .OrderByDescending(v => v.Date)
                .Take(10)
                .AsNoTracking()
                .ToListAsync();

            return Ok(values);
        }
    }
}
