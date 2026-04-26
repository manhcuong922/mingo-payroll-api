namespace MPS.API.Requests.PerformanceMetric;

public sealed record UpdatePerformanceMetricRequest(
    string Name,
    string Description,
    decimal Weight,
    int TypePerformanceMetricId,
    IReadOnlyList<int> TargetRoleIds);
