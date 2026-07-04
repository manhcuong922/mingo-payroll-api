using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveType.Queries;

public class GetLeaveTypeByIdQueryHandler : IRequestHandler<GetLeaveTypeByIdQuery, LeaveTypeDto?>
{
    private readonly ILeaveTypeRepository _repository;

    public GetLeaveTypeByIdQueryHandler(ILeaveTypeRepository repository)
    {
        _repository = repository;
    }

    public async Task<LeaveTypeDto?> Handle(GetLeaveTypeByIdQuery request, CancellationToken cancellationToken)
    {
        var t = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (t is null) return null;

        return new LeaveTypeDto(
            t.Id,
            t.Code,
            t.Name,
            t.Description,
            t.IsPaid,
            t.PaidRate,
            t.MaxDaysPerRequest,
            t.AllowEmployeeSelection,
            t.MaxDaysPerYear,
            t.MaxDaysPerMonth,
            t.MaxDaysPerWeek,
            t.ApplyScope,
            t.TargetDepartmentIds,
            t.TargetEmployeeIds,
            t.RequireDocument,
            t.DeductsBalance,
            t.IsActive,
            t.CreatedAt,
            t.UpdatedAt);
    }
}
