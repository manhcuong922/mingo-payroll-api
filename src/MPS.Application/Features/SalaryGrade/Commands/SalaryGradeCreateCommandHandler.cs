using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Commands;

public class SalaryGradeCreateCommandHandler : IRequestHandler<SalaryGradeCreateCommand, int>
{
    private readonly ISalaryGradeRepository _repository;

    public SalaryGradeCreateCommandHandler(ISalaryGradeRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(SalaryGradeCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.SalaryGrade
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            PositionId = request.PositionId,
            Level = request.Level,
            BaseSalary = request.BaseSalary,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
