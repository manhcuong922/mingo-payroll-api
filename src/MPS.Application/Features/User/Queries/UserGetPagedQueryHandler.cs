using AutoMapper;
using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Queries;

public class UserGetPagedQueryHandler : IRequestHandler<UserGetPagedQuery, PagedResult<UserDto>>
{
    private readonly IUserRepository _repository;
    private readonly IMapper _mapper;

    public UserGetPagedQueryHandler(IUserRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<UserDto>> Handle(UserGetPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Keyword,
            request.RoleId,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<UserDto>>(items);
        return new PagedResult<UserDto>(dtos, total, request.Page, request.PageSize);
    }
}
