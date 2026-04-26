namespace MPS.Domain.Entities
{
    public class PerformanceMetric
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public int TypePerformanceMetricId { get; set; }
        public TypePerformanceMetric TypePerformanceMetric { get; set; } = null!;
        public List<Role> TargetRoles { get; set; } = new();
    }
}
