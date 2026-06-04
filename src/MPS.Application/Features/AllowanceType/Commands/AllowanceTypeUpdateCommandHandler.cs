using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.AllowanceType.Commands;

public class AllowanceTypeUpdateCommandHandler : IRequestHandler<AllowanceTypeUpdateCommand, bool>
{
    private readonly IAllowanceTypeRepository _repository;

    public AllowanceTypeUpdateCommandHandler(IAllowanceTypeRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(AllowanceTypeUpdateCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.AllowanceType
        {
            Id = request.Id,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            DefaultAmount = request.DefaultAmount,
            IsBhxhBase = request.IsBhxhBase,
            IsTaxable = request.IsTaxable,
            IsActive = request.IsActive,
        };
        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
