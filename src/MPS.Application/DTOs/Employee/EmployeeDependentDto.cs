using MPS.Domain.Enums.Employee;

namespace MPS.Application.DTOs.Employee;

public sealed record EmployeeDependentDto(
    int Id,
    int EmployeeInformationId,
    string FullName,
    DateOnly DateOfBirth,
    string? Gender,
    DependentRelationship Relationship,
    string? TaxIdentificationNumber,
    string? CitizenId,
    DateOnly RegisteredFromDate,
    DateOnly? RegisteredToDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record DependentRegistrationRequestDto(
    int Id,
    int EmployeeInformationId,
    string EmployeeFullName,
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
    DependentRequestStatus Status,
    string? Reason,
    string? ReviewNote,
    int? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime? CancelledAt,
    int? CancelledByUserId,
    string? CancelReason,
    DateTime CreatedAt);
