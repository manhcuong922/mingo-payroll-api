using LeaveTypeEntity = MPS.Domain.Entities.Attendance.LeaveType;

namespace MPS.Application.Features.Attendance.LeaveType.Common;

internal static class LeaveTypeAssignmentPolicy
{
    public static bool TracksYearlyBalance(LeaveTypeEntity leaveType) =>
        leaveType.DeductsBalance || leaveType.MaxDaysPerYear.HasValue;

    public static decimal ResolveYearlyQuota(LeaveTypeEntity leaveType) =>
        leaveType.MaxDaysPerYear ?? leaveType.MaxDaysPerRequest ?? 0m;

    public static bool AppliesToEmployee(LeaveTypeEntity leaveType, int employeeInformationId)
    {
        return NormalizeScope(leaveType.ApplyScope) switch
        {
            "AllEmployees" => true,
            // Department entities are not implemented yet; until then, all departments means all current employees.
            "AllDepartments" => true,
            "SpecificEmployees" => ParseIds(leaveType.TargetEmployeeIds).Contains(employeeInformationId),
            "SpecificDepartments" => false,
            _ => false
        };
    }

    public static IReadOnlyList<int> ResolveTargetEmployeeIds(
        LeaveTypeEntity leaveType,
        IReadOnlyCollection<int> allEmployeeInformationIds)
    {
        var allIds = allEmployeeInformationIds.Distinct().ToHashSet();

        return NormalizeScope(leaveType.ApplyScope) switch
        {
            "AllEmployees" => allIds.OrderBy(id => id).ToList(),
            "AllDepartments" => allIds.OrderBy(id => id).ToList(),
            "SpecificEmployees" => ParseIds(leaveType.TargetEmployeeIds)
                .Where(allIds.Contains)
                .Distinct()
                .OrderBy(id => id)
                .ToList(),
            "SpecificDepartments" => Array.Empty<int>(),
            _ => Array.Empty<int>()
        };
    }

    private static string NormalizeScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? "AllEmployees" : scope.Trim();

    private static IReadOnlySet<int> ParseIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new HashSet<int>();

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
    }
}
