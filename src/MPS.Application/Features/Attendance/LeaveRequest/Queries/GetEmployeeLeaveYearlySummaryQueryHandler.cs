using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public class GetEmployeeLeaveYearlySummaryQueryHandler
    : IRequestHandler<GetEmployeeLeaveYearlySummaryQuery, IReadOnlyList<EmployeeLeaveYearlySummaryDto>>
{
    private readonly ILeaveRequestRepository _leaveRequestRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;

    public GetEmployeeLeaveYearlySummaryQueryHandler(
        ILeaveRequestRepository leaveRequestRepository,
        IEmployeeInformationRepository employeeRepository)
    {
        _leaveRequestRepository = leaveRequestRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<IReadOnlyList<EmployeeLeaveYearlySummaryDto>> Handle(
        GetEmployeeLeaveYearlySummaryQuery request,
        CancellationToken cancellationToken)
    {
        var employees = await _employeeRepository.GetAllAsync(cancellationToken: cancellationToken);
        var monthlyRows = await _leaveRequestRepository.GetYearlyEmployeeSummaryAsync(request.Year, cancellationToken);
        var monthlyByEmployee = monthlyRows
            .GroupBy(row => row.EmployeeInformationId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.Month));

        return employees
            .OrderBy(employee => employee.FullName)
            .Select(employee =>
            {
                var approvedByMonth = Enumerable.Repeat(0m, 12).ToArray();
                var approvedCount = 0;
                var pendingDays = 0m;
                var pendingCount = 0;

                if (monthlyByEmployee.TryGetValue(employee.Id, out var monthly))
                {
                    for (var month = 1; month <= 12; month++)
                    {
                        if (!monthly.TryGetValue(month, out var row)) continue;

                        approvedByMonth[month - 1] = row.ApprovedDays;
                        approvedCount += row.ApprovedRequestCount;
                        pendingDays += row.PendingDays;
                        pendingCount += row.PendingRequestCount;
                    }
                }

                return new EmployeeLeaveYearlySummaryDto(
                    employee.Id,
                    employee.FullName,
                    employee.User?.EmployeeCode,
                    request.Year,
                    approvedByMonth,
                    approvedByMonth.Sum(),
                    approvedCount,
                    pendingDays,
                    pendingCount);
            })
            .ToList();
    }
}
