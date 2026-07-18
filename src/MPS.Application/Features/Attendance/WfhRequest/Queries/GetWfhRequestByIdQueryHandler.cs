using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.WfhRequest.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.WfhRequest.Queries;

public class GetWfhRequestByIdQueryHandler : IRequestHandler<GetWfhRequestByIdQuery, WfhRequestDto?>
{
    private readonly IWfhRequestRepository _repository;

    public GetWfhRequestByIdQueryHandler(IWfhRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<WfhRequestDto?> Handle(GetWfhRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return r is null ? null : WfhRequestMapper.ToDto(r);
    }
}
