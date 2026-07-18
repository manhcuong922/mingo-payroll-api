using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.LeaveRequest.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public class GetLeaveRequestByIdQueryHandler : IRequestHandler<GetLeaveRequestByIdQuery, LeaveRequestDto?>
{
    private readonly ILeaveRequestRepository _repository;

    public GetLeaveRequestByIdQueryHandler(ILeaveRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<LeaveRequestDto?> Handle(GetLeaveRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return r is null ? null : LeaveRequestMapper.ToDto(r);
    }
}
