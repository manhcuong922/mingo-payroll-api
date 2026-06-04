using MediatR;

namespace MPS.Application.Features.SalaryGrade.Commands;

public sealed record SalaryGradeCreateCommand(
    string Code,
    string Name,
    int? PositionId,
    int? Level,
    decimal BaseSalary,
    string Currency,
    string? Description,
    bool IsActive) : IRequest<int>;
