using MediatR;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Commands;

public class SalaryGradeCreateCommandHandler : IRequestHandler<SalaryGradeCreateCommand, int>
{
    private readonly ISalaryGradeRepository _repository;
    private readonly IPositionRepository _positionRepository;

    public SalaryGradeCreateCommandHandler(
        ISalaryGradeRepository repository,
        IPositionRepository positionRepository)
    {
        _repository = repository;
        _positionRepository = positionRepository;
    }

    public async Task<int> Handle(SalaryGradeCreateCommand request, CancellationToken cancellationToken)
    {
        var position = await _positionRepository.GetByIdAsync(request.PositionId, cancellationToken)
            ?? throw new DomainValidationException($"Position {request.PositionId} was not found.");

        var entity = new Domain.Entities.SalaryGrade
        {
            Code = $"{position.Code}-{request.Level}",
            Name = request.Name.Trim(),
            PositionId = position.Id,
            Level = request.Level,
            BaseSalary = request.BaseSalary,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Description = request.Description?.Trim(),
            IsActive = true,
        };

        return await _repository.CreateWithDeactivationAsync(entity, cancellationToken);
    }
}
