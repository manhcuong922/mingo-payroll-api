using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.SalaryGrade.Queries;

public sealed record SalaryGradeGetAllQuery(int? PositionId, bool? IsActive)
    : IRequest<IReadOnlyList<SalaryGradeDto>>;
