using MediatR;

namespace MPS.Application.Features.SalaryGrade.Commands;

public sealed record SalaryGradeDeleteCommand(int Id) : IRequest<bool>;
