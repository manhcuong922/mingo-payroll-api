using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;

namespace MPS.Application.Features.SalaryGrade.Queries;

public sealed record SalaryGradeGetPagedQuery(
    int? PositionId,
    bool? IsActive,
    int Page,
    int PageSize) : IRequest<PagedResult<SalaryGradeDto>>;
