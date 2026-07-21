using MediatR;
using MPS.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPS.Application.Features.EmployeeInformation.Queries;
/// <param name="Keyword">Case+accent-insensitive LIKE trên FullName / User.EmployeeCode / Department.Name.</param>
public record EmployeeInformationGetAllQuery(
    bool? HasActiveUser = null,
    string? Keyword = null)
    : IRequest<IReadOnlyList<EmployeeInformationDto>>;

