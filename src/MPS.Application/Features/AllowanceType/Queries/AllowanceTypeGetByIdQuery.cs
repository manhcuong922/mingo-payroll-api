using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.AllowanceType.Queries;

public sealed record AllowanceTypeGetByIdQuery(int Id) : IRequest<AllowanceTypeDto?>;
