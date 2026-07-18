using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.LateEarlyRequest.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Queries;

public class GetLateEarlyRequestByIdQueryHandler : IRequestHandler<GetLateEarlyRequestByIdQuery, LateEarlyRequestDto?>
{
    private readonly ILateEarlyRequestRepository _repository;

    public GetLateEarlyRequestByIdQueryHandler(ILateEarlyRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<LateEarlyRequestDto?> Handle(GetLateEarlyRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return r is null ? null : LateEarlyRequestMapper.ToDto(r);
    }
}
