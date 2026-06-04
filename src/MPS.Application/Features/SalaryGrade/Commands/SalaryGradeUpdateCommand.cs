using MediatR;

namespace MPS.Application.Features.SalaryGrade.Commands;

public sealed record SalaryGradeUpdateCommand(
    int Id,
    string Code,
    string Name,
    int? PositionId,
    int? Level,
    decimal BaseSalary,
    string Currency,
    string? Description,
    bool IsActive) : IRequest<bool>;
