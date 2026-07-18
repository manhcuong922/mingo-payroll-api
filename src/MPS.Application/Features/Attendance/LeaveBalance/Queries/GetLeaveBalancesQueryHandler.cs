using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveBalance.Queries;

public class GetLeaveBalancesQueryHandler : IRequestHandler<GetLeaveBalancesQuery, IReadOnlyList<LeaveBalanceDto>>
{
    private readonly ILeaveBalanceRepository _repository;

    public GetLeaveBalancesQueryHandler(ILeaveBalanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<LeaveBalanceDto>> Handle(GetLeaveBalancesQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetByEmployeeAsync(request.EmployeeInformationId, request.Year, cancellationToken);
        return items.Select(b => new LeaveBalanceDto(
            b.Id, b.EmployeeInformationId, b.Year,
            b.LeaveTypeId, b.LeaveType?.Code ?? string.Empty, b.LeaveType?.Name ?? string.Empty,
            b.BaseDays, b.SeniorityBonusDays, b.TotalDays,
            b.UsedDays, b.TotalDays - b.UsedDays)).ToList();
    }
}
