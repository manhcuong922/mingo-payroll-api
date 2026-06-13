using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;

namespace MPS.Application.Features.AllowanceType.Queries;

public sealed record AllowanceTypeGetPagedQuery(
    bool? IsActive,
    int Page,
    int PageSize) : IRequest<PagedResult<AllowanceTypeDto>>;
