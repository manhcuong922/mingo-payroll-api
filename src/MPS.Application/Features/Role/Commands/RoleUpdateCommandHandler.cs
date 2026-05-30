using MediatR;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Role.Commands;

public sealed class RoleUpdateCommandHandler : IRequestHandler<RoleUpdateCommand, bool>
{
    private readonly IRoleRepository _roleRepository;

    public RoleUpdateCommandHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<bool> Handle(RoleUpdateCommand request, CancellationToken cancellationToken)
    {
        var existing = await _roleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.IsSystem)
            throw new SystemRoleModificationException(
                "System roles cannot have their name or description modified. Update permissions instead.");

        existing.Name = request.Name;
        existing.Description = request.Description;

        return await _roleRepository.UpdateAsync(existing, cancellationToken);
    }
}
