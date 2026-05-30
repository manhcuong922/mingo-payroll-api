using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.Position.Commands;
public record PositionUpdateCommand(int Id, string Name) : IRequest<bool>;
