using LeaveTypeEntity = MPS.Domain.Entities.Attendance.LeaveType;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;

namespace MPS.Application.Features.Attendance.LeaveType.Common;

internal static class LeaveTypeAssignmentPolicy
{
    public static bool TracksYearlyBalance(LeaveTypeEntity leaveType) =>
        leaveType.DeductsBalance || leaveType.MaxDaysPerYear.HasValue;

    public static decimal ResolveYearlyQuota(LeaveTypeEntity leaveType) =>
        leaveType.MaxDaysPerYear ?? leaveType.MaxDaysPerRequest ?? 0m;

    public static bool AppliesToEmployee(
        LeaveTypeEntity leaveType,
        int employeeInformationId,
        int? departmentId)
    {
        return NormalizeScope(leaveType.ApplyScope) switch
        {
            "AllEmployees" => true,
            "AllDepartments" => departmentId.HasValue,
            "SpecificEmployees" => ParseIds(leaveType.TargetEmployeeIds).Contains(employeeInformationId),
            "SpecificDepartments" => departmentId.HasValue
                && ParseIds(leaveType.TargetDepartmentIds).Contains(departmentId.Value),
            _ => false
        };
    }

    public static IReadOnlyList<int> ResolveTargetEmployeeIds(
        LeaveTypeEntity leaveType,
        IReadOnlyCollection<EmployeeInformationEntity> employees)
    {
        var employeesById = employees
            .GroupBy(employee => employee.Id)
            .Select(group => group.First())
            .ToList();
        var allIds = employeesById.Select(employee => employee.Id).ToHashSet();
        var targetDepartmentIds = ParseIds(leaveType.TargetDepartmentIds);

        return NormalizeScope(leaveType.ApplyScope) switch
        {
            "AllEmployees" => allIds.OrderBy(id => id).ToList(),
            "AllDepartments" => employeesById
                .Where(employee => employee.DepartmentId.HasValue)
                .Select(employee => employee.Id)
                .OrderBy(id => id)
                .ToList(),
            "SpecificEmployees" => ParseIds(leaveType.TargetEmployeeIds)
                .Where(allIds.Contains)
                .Distinct()
                .OrderBy(id => id)
                .ToList(),
            "SpecificDepartments" => employeesById
                .Where(employee => employee.DepartmentId.HasValue
                    && targetDepartmentIds.Contains(employee.DepartmentId.Value))
                .Select(employee => employee.Id)
                .OrderBy(id => id)
                .ToList(),
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
