namespace MPS.Domain.Entities
{
    public class TypePerformanceMetric
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public List<PerformanceMetric> PerformanceMetrics { get; set; } = new();
    }
}
