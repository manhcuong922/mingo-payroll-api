using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.AllowanceType.Queries;

public sealed record AllowanceTypeGetAllQuery(bool? IsActive)
    : IRequest<IReadOnlyList<AllowanceTypeDto>>;
