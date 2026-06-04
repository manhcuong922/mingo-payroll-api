using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Commands;

public class SalaryGradeDeleteCommandHandler : IRequestHandler<SalaryGradeDeleteCommand, bool>
{
    private readonly ISalaryGradeRepository _repository;

    public SalaryGradeDeleteCommandHandler(ISalaryGradeRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> Handle(SalaryGradeDeleteCommand request, CancellationToken cancellationToken) =>
        _repository.DeleteOrDeactivateAsync(request.Id, cancellationToken);
}
