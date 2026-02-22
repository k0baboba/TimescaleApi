namespace TimescaleApi.Models
{
    public class Result
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public double DeltaTimeSeconds { get; set; }
        public DateTime MinDate { get; set; }
        public double AverageExecutionTime { get; set; }
        public double AverageValue { get; set; }
        public double MedianValue { get; set; }
        public double MaxValue { get; set; }
        public double MinValue { get; set; }

    }
}
