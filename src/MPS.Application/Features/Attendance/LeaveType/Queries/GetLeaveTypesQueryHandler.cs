using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveType.Queries;

public class GetLeaveTypesQueryHandler : IRequestHandler<GetLeaveTypesQuery, PagedResult<LeaveTypeDto>>
{
    private readonly ILeaveTypeRepository _repository;

    public GetLeaveTypesQueryHandler(ILeaveTypeRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<LeaveTypeDto>> Handle(GetLeaveTypesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.GetPagedAsync(request.IsActive, request.Search, page, size, cancellationToken);

        var dtos = items.Select(t => new LeaveTypeDto(
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
            t.UpdatedAt)).ToList();

        return new PagedResult<LeaveTypeDto>(dtos, total, page, size);
    }
}
