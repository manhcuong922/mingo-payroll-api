using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Common;

internal static class ScheduleTemplateEmployeeResolver
{
    public static async Task<IReadOnlyList<int>> ResolveAsync(
        ScheduleApplyTarget? applyTarget,
        IReadOnlyList<int>? employeeInformationIds,
        IReadOnlyList<int>? departmentIds,
        IEmployeeInformationRepository employeeRepository,
        bool requireEmployees,
        CancellationToken cancellationToken)
    {
        var target = applyTarget ?? ScheduleApplyTarget.Employees;
        return target switch
        {
            ScheduleApplyTarget.Employees => ResolveEmployeeIds(employeeInformationIds),
            ScheduleApplyTarget.Departments => await ResolveDepartmentEmployeeIdsAsync(
                departmentIds,
                employeeRepository,
                requireEmployees,
                cancellationToken),
            _ => throw new DomainValidationException("Đối tượng áp dụng phân ca không hợp lệ.")
        };
    }

    private static IReadOnlyList<int> ResolveEmployeeIds(IReadOnlyList<int>? employeeInformationIds)
    {
        if (employeeInformationIds is null || employeeInformationIds.Count == 0)
        {
            return Array.Empty<int>();
        }

        if (employeeInformationIds.Any(id => id <= 0))
        {
            throw new DomainValidationException("EmployeeInformationIds phải lớn hơn 0.");
        }

        return employeeInformationIds
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    private static async Task<IReadOnlyList<int>> ResolveDepartmentEmployeeIdsAsync(
        IReadOnlyList<int>? departmentIds,
        IEmployeeInformationRepository employeeRepository,
        bool requireEmployees,
        CancellationToken cancellationToken)
    {
        if (departmentIds is null || departmentIds.Count == 0)
        {
            if (requireEmployees)
            {
                throw new DomainValidationException("Chọn ít nhất 1 phòng ban áp dụng.");
            }

            return Array.Empty<int>();
        }

        if (departmentIds.Any(id => id <= 0))
        {
            throw new DomainValidationException("DepartmentIds phải lớn hơn 0.");
        }

        var departmentSet = departmentIds
            .Distinct()
            .ToHashSet();

        var employees = await employeeRepository.GetAllAsync(
            hasActiveUser: true,
            cancellationToken: cancellationToken);
        var employeeIds = employees
            .Where(employee => employee.DepartmentId.HasValue
                && departmentSet.Contains(employee.DepartmentId.Value))
            .Select(employee => employee.Id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (employeeIds.Count == 0)
        {
            throw new DomainValidationException(
                "Phòng ban đã chọn chưa có nhân viên đang có hợp đồng active.");
        }

        return employeeIds;
    }
}
