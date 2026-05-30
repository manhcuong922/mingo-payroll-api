using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Queries;

public class UserGetByEmployeeCodeQueryHandler : IRequestHandler<UserGetByEmployeeCodeQuery, UserDto?>
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;

    public UserGetByEmployeeCodeQueryHandler(IUserRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<UserDto?> Handle(UserGetByEmployeeCodeQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByEmployeeCodeAsync(request.EmployeeCode, cancellationToken);
        return entity is null ? null : _mapper.Map<UserDto>(entity);
    }
}
