using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.AllowanceType.Commands;

public class AllowanceTypeCreateCommandHandler : IRequestHandler<AllowanceTypeCreateCommand, int>
{
    private readonly IAllowanceTypeRepository _repository;

    public AllowanceTypeCreateCommandHandler(IAllowanceTypeRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(AllowanceTypeCreateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.AllowanceType
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            DefaultAmount = request.DefaultAmount,
            IsBhxhBase = request.IsBhxhBase,
            IsTaxable = request.IsTaxable,
            IsActive = request.IsActive,
        };
        return await _repository.AddAsync(entity, cancellationToken);
    }
}
