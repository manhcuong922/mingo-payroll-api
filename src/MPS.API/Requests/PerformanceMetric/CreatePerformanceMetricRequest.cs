namespace MPS.API.Requests.PerformanceMetric;

public sealed record CreatePerformanceMetricRequest(
    string Name,
    string Description,
    decimal Weight,
    int TypePerformanceMetricId,
    IReadOnlyList<int> TargetRoleIds);
