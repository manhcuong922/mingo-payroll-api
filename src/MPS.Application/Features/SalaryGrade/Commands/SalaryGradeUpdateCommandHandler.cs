using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Commands;

public class SalaryGradeUpdateCommandHandler : IRequestHandler<SalaryGradeUpdateCommand, bool>
{
    private readonly ISalaryGradeRepository _repository;

    public SalaryGradeUpdateCommandHandler(ISalaryGradeRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(SalaryGradeUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.SalaryGrade
        {
            Id = request.Id,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            PositionId = request.PositionId,
            Level = request.Level,
            BaseSalary = request.BaseSalary,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
