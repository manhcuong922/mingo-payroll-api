using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;

namespace MPS.Application.Features.EmployeeInformation.Queries;

public sealed record EmployeeInformationGetPagedQuery(
    string? Keyword,
    int? PositionId,
    bool? HasAccount,
    bool? HasActiveUser,
    int Page,
    int PageSize) : IRequest<PagedResult<EmployeeInformationDto>>;
