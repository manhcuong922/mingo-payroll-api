using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Role.Queries;

public sealed class RoleGetByIdQueryHandler : IRequestHandler<RoleGetByIdQuery, RoleDetailDto?>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IMapper _mapper;

    public RoleGetByIdQueryHandler(IRoleRepository roleRepository, IMapper mapper)
    {
        _roleRepository = roleRepository;
        _mapper = mapper;
    }

    public async Task<RoleDetailDto?> Handle(RoleGetByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdWithPermissionsAsync(request.Id, cancellationToken);
        return role is null ? null : _mapper.Map<RoleDetailDto>(role);
    }
}
