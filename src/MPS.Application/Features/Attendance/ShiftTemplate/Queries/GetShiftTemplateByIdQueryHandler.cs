using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Queries;

public class GetShiftTemplateByIdQueryHandler : IRequestHandler<GetShiftTemplateByIdQuery, ShiftTemplateDto?>
{
    private readonly IShiftTemplateRepository _repository;

    public GetShiftTemplateByIdQueryHandler(IShiftTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<ShiftTemplateDto?> Handle(GetShiftTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : ShiftTemplateMapper.ToDto(entity);
    }
}
