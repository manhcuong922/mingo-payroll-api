using MediatR;
using MPS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.Position.Queries;
public record PositionGetByIdQuery(int Id) : IRequest<PositionDto?>;

