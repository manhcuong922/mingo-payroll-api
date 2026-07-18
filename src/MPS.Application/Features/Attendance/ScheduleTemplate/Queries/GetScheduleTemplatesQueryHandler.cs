using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.ScheduleTemplate.Common;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Queries;

public class GetScheduleTemplatesQueryHandler
    : IRequestHandler<GetScheduleTemplatesQuery, PagedResult<ScheduleTemplateDto>>
{
    private readonly IScheduleTemplateRepository _repository;

    public GetScheduleTemplatesQueryHandler(IScheduleTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<ScheduleTemplateDto>> Handle(
        GetScheduleTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.Size is < 1 or > 100 ? 20 : request.Size;

        var (items, total) = await _repository.GetPagedAsync(
            request.IsActive, request.Keyword, page, size, cancellationToken);

        var dtos = items.Select(ScheduleTemplateMapper.ToDto).ToList();
        return new PagedResult<ScheduleTemplateDto>(dtos, total, page, size);
    }
}
