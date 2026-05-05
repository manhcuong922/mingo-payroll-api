using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.User.Commands;

public record UserDeleteCommand(string EmployeeCode) : IRequest<bool>;

