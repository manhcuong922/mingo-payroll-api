using MediatR;
using MPS.Domain.Enums.Employee;

namespace MPS.Application.Features.Employee.Dependent.Commands;

public sealed record CreateDependentRegistrationRequestCommand(
    DependentRequestType RequestType,
    int? TargetDependentId,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    DependentRelationship? Relationship,
    string? TaxIdentificationNumber,
    string? CitizenId,
    DateOnly? RegisteredFromDate,
    DateOnly? RegisteredToDate,
    string? SupportingDocumentUrl,
    string? Reason) : IRequest<int>;
