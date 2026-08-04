using MPS.Domain.Enums.Employee;

namespace MPS.API.Requests.Employee;

public sealed record CreateDependentRegistrationRequest(
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
    DependentIdentityDocumentType? IdentityDocumentType,
    string? IdentityDocumentNumber,
    DateOnly? IdentityDocumentIssuedDate,
    string? Reason);

public sealed record ApproveDependentRegistrationBody(string? ReviewNote);

public sealed record RejectDependentRegistrationBody(string? ReviewNote);

public sealed record CancelDependentRegistrationBody(string? CancelReason);
