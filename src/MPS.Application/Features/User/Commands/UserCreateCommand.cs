using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.User.Commands;


public record UserCreateCommand(
    string EmployeeCode,
    string FullName,
    int RoleId,
    string Username,
    string Password,
    string Email,
    DateTime Dob,
    string PhoneNo,
    int Status) : IRequest<string>;