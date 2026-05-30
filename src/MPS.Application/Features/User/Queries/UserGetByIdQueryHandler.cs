using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Queries;

public class UserGetByIdQueryHandler : IRequestHandler<UserGetByIdQuery, UserDto?>
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public UserGetByIdQueryHandler(
        IUserRepository repository,
        IMapper mapper,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
        _mapper = mapper;
        _currentUserService = currentUserService;
    }

    public async Task<UserDto?> Handle(UserGetByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasPermission(Permissions.UserReadAll)
            && _currentUserService.UserId != request.Id)
        {
            throw new ForbiddenAccessException("You can only view your own user info.");
        }

        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<UserDto>(entity);
    }
}
