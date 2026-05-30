using MediatR;
using MPS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.EmployeeInformation.Queries;
public record EmployeeInformationGetByIdQuery(int Id) : IRequest<EmployeeInformationDto?>;
