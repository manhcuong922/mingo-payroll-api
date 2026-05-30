using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Queries;

public class UserGetByIdQueryHandler : IRequestHandler<UserGetByIdQuery, UserDto?>
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;

    public UserGetByIdQueryHandler(IUserRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<UserDto?> Handle(UserGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<UserDto>(entity);
    }
}
