using MediatR;

namespace MPS.Application.Features.SalaryGrade.Commands;

public sealed record SalaryGradeCreateCommand(
    string Name,
    int PositionId,
    int Level,
    decimal BaseSalary,
    string Currency,
    string? Description) : IRequest<int>;
