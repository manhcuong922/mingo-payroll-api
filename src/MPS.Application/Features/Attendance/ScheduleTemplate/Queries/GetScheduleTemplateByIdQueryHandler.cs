using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.ScheduleTemplate.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Queries;

public class GetScheduleTemplateByIdQueryHandler
    : IRequestHandler<GetScheduleTemplateByIdQuery, ScheduleTemplateDto?>
{
    private readonly IScheduleTemplateRepository _repository;

    public GetScheduleTemplateByIdQueryHandler(IScheduleTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<ScheduleTemplateDto?> Handle(
        GetScheduleTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : ScheduleTemplateMapper.ToDto(entity);
    }
}
