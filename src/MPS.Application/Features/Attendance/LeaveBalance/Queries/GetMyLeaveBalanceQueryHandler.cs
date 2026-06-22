using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveBalance.Queries;

public class GetMyLeaveBalanceQueryHandler : IRequestHandler<GetMyLeaveBalanceQuery, IReadOnlyList<LeaveBalanceDto>>
{
    private readonly ILeaveBalanceRepository _repository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyLeaveBalanceQueryHandler(
        ILeaveBalanceRepository repository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _repository = repository;
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
        return items.Select(b => new LeaveBalanceDto(
            b.Id, b.EmployeeInformationId, b.Year,
            b.LeaveTypeId, b.LeaveType?.Code ?? string.Empty, b.LeaveType?.Name ?? string.Empty,
            b.BaseDays, b.SeniorityBonusDays, b.TotalDays,
            b.UsedDays, b.TotalDays - b.UsedDays)).ToList();
    }
}
