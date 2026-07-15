using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Queries;

public class GetShiftTemplateByIdQueryHandler : IRequestHandler<GetShiftTemplateByIdQuery, ShiftTemplateDto?>
{
    private readonly IShiftTemplateRepository _repository;
    private readonly IAttendanceRuleVersionRepository _versionRepository;

    public GetShiftTemplateByIdQueryHandler(
        IShiftTemplateRepository repository,
        IAttendanceRuleVersionRepository versionRepository)
    {
        _repository = repository;
        _versionRepository = versionRepository;
    }

    public async Task<ShiftTemplateDto?> Handle(GetShiftTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null) return null;

        var openVersion = await _versionRepository.GetOpenAsync(entity.AppliedAttendanceRuleId, cancellationToken);
        return ShiftTemplateMapper.ToDto(entity, openVersion);
    }
}
