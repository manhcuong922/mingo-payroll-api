using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public class GetOvertimeRequestByIdQueryHandler : IRequestHandler<GetOvertimeRequestByIdQuery, OvertimeRequestDto?>
{
    private readonly IOvertimeRequestRepository _repository;

    public GetOvertimeRequestByIdQueryHandler(IOvertimeRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<OvertimeRequestDto?> Handle(GetOvertimeRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return r is null ? null : OvertimeRequestMapper.ToDto(r);
    }
}
