using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.User.Queries;
public class UserGetAllQueryHandler : IRequestHandler<UserGetAllQuery, IReadOnlyList<UserDto>>
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;

    public UserGetAllQueryHandler(IUserRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<UserDto>> Handle(UserGetAllQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<UserDto>>(entities);
    }
}