using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.WeeklyDayOffRule.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Queries;

public class ResolveWeeklyDayOffQueryHandler : IRequestHandler<ResolveWeeklyDayOffQuery, WeeklyDayOffResolveDto>
{
    private readonly IWeeklyDayOffRuleRepository _repository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IDayTypeResolver _dayTypeResolver;

    public ResolveWeeklyDayOffQueryHandler(
        IWeeklyDayOffRuleRepository repository,
        IEmployeeInformationRepository employeeRepository,
        IDayTypeResolver dayTypeResolver)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _dayTypeResolver = dayTypeResolver;
    }

    public async Task<WeeklyDayOffResolveDto> Handle(ResolveWeeklyDayOffQuery request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeInformationId, cancellationToken)
            ?? throw new NotFoundException($"Employee {request.EmployeeInformationId} not found.");

        var rules = await _repository.FindActiveOnDateAsync(request.Date, cancellationToken);
        var applicable = rules.Where(r => AppliesTo(r.ApplyScope, r.TargetDepartmentIds, r.TargetEmployeeIds,
                request.EmployeeInformationId, employee.DepartmentId))
            .SelectMany(r => DaysOfWeekMask.Decode(r.DaysOfWeekMask))
            .Distinct()
            .OrderBy(d => d)
            .ToArray();

        var dayType = await _dayTypeResolver.ResolveAsync(request.EmployeeInformationId, request.Date, cancellationToken);
        return new WeeklyDayOffResolveDto(
            request.EmployeeInformationId,
            request.Date,
            applicable,
            dayType == DayType.WeeklyRestDay);
    }

    private static bool AppliesTo(
        string? applyScope,
        string? targetDepartmentIds,
        string? targetEmployeeIds,
        int employeeInformationId,
        int? departmentId) =>
        NormalizeScope(applyScope) switch
        {
            "AllEmployees" => true,
            "AllDepartments" => departmentId.HasValue,
            "SpecificEmployees" => ParseIds(targetEmployeeIds).Contains(employeeInformationId),
            "SpecificDepartments" => departmentId.HasValue
                && ParseIds(targetDepartmentIds).Contains(departmentId.Value),
            _ => false
        };

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
