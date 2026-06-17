using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Queries;

public sealed record GetShiftTemplatesQuery(
    bool? IsActive,
    int Page,
    int Size) : IRequest<PagedResult<ShiftTemplateDto>>;
