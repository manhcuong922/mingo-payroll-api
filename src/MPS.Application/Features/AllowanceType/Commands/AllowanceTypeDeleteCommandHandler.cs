using MediatR;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.AllowanceType.Commands;

public class AllowanceTypeDeleteCommandHandler : IRequestHandler<AllowanceTypeDeleteCommand, bool>
{
    private readonly IAllowanceTypeRepository _repository;

    public AllowanceTypeDeleteCommandHandler(IAllowanceTypeRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> Handle(AllowanceTypeDeleteCommand request, CancellationToken cancellationToken) =>
        _repository.DeleteOrDeactivateAsync(request.Id, cancellationToken);
}
