using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.LeaveType.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveBalance.Queries;

public class GetMyLeaveBalanceQueryHandler : IRequestHandler<GetMyLeaveBalanceQuery, IReadOnlyList<LeaveBalanceDto>>
{
    private readonly ILeaveBalanceRepository _repository;
    private readonly ILeaveTypeRepository _leaveTypeRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyLeaveBalanceQueryHandler(
        ILeaveBalanceRepository repository,
        ILeaveTypeRepository leaveTypeRepository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _leaveTypeRepository = leaveTypeRepository;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<LeaveBalanceDto>> Handle(GetMyLeaveBalanceQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var items = await _repository.GetByEmployeeAsync(employeeId, request.Year, cancellationToken);
        var activeTypes = await _leaveTypeRepository.GetActiveAsync(cancellationToken);
        var assignedTypes = activeTypes
            .Where(t => LeaveTypeAssignmentPolicy.TracksYearlyBalance(t)
                && LeaveTypeAssignmentPolicy.AppliesToEmployee(t, employeeId))
            .ToDictionary(t => t.Id);

        var result = items
            .Where(b => assignedTypes.ContainsKey(b.LeaveTypeId))
            .Select(b =>
            {
                var leaveType = assignedTypes[b.LeaveTypeId];
                var totalDays = b.TotalDays > 0m
                    ? b.TotalDays
                    : LeaveTypeAssignmentPolicy.ResolveYearlyQuota(leaveType);
                return new LeaveBalanceDto(
                    b.Id, b.EmployeeInformationId, b.Year,
                    b.LeaveTypeId, b.LeaveType?.Code ?? leaveType.Code, b.LeaveType?.Name ?? leaveType.Name,
                    b.BaseDays > 0m ? b.BaseDays : totalDays, b.SeniorityBonusDays, totalDays,
                    b.UsedDays, totalDays - b.UsedDays);
            })
            .ToList();

        var existingTypeIds = result.Select(b => b.LeaveTypeId).ToHashSet();
        foreach (var leaveType in assignedTypes.Values.Where(t => !existingTypeIds.Contains(t.Id)))
        {
            var totalDays = LeaveTypeAssignmentPolicy.ResolveYearlyQuota(leaveType);
            if (totalDays <= 0m) continue;

            result.Add(new LeaveBalanceDto(
                0, employeeId, request.Year,
                leaveType.Id, leaveType.Code, leaveType.Name,
                totalDays, 0m, totalDays,
                0m, totalDays));
        }

        return result
            .OrderBy(item => item.LeaveTypeCode)
            .ToList();
    }
}
