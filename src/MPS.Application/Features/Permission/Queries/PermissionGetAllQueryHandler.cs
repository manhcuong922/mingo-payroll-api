using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Permission.Queries;

public sealed class PermissionGetAllQueryHandler : IRequestHandler<PermissionGetAllQuery, IReadOnlyList<PermissionDto>>
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMapper _mapper;

    public PermissionGetAllQueryHandler(IPermissionRepository permissionRepository, IMapper mapper)
    {
        _permissionRepository = permissionRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<PermissionDto>> Handle(PermissionGetAllQuery request, CancellationToken cancellationToken)
    {
        var permissions = await _permissionRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<PermissionDto>>(permissions);
    }
}
