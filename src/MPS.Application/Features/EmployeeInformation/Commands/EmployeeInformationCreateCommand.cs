using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.EmployeeInformation.Commands;
public record EmployeeInformationCreateCommand(
    string EmployeeCode,
    string TaxNo,
    string SocialNo,
    string BankNumber,
    int PositionId,
    int DependentsNo,
    string CitizenId,
    string Address) : IRequest<int>;
