using MediatR;

namespace MPS.Application.Features.SalaryGrade.Commands;

public sealed record SalaryGradeUpdateCommand(
    int Id,
    string Name,
    decimal BaseSalary,
    string Currency,
    string? Description,
    bool IsActive) : IRequest<bool>;
