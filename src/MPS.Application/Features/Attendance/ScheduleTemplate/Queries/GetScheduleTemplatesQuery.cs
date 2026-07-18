using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Queries;

public sealed record GetScheduleTemplatesQuery(
    bool? IsActive,
    string? Keyword,
    int Page,
    int Size) : IRequest<PagedResult<ScheduleTemplateDto>>;
