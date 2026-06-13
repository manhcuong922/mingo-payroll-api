using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;

namespace MPS.Application.Features.User.Queries;

public sealed record UserGetPagedQuery(
    string? Keyword,
    int? RoleId,
    int? Status,
    int Page,
    int PageSize) : IRequest<PagedResult<UserDto>>;
