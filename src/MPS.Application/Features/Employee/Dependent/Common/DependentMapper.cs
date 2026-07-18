using MPS.Application.DTOs.Employee;
using DependentEntity = MPS.Domain.Entities.Employee.EmployeeDependent;
using RequestEntity = MPS.Domain.Entities.Employee.DependentRegistrationRequest;

namespace MPS.Application.Features.Employee.Dependent.Common;

internal static class DependentMapper
{
    public static EmployeeDependentDto ToDto(DependentEntity d) => new(
        d.Id,
        d.EmployeeInformationId,
        d.FullName,
        d.DateOfBirth,
        d.Gender,
        d.Relationship,
        d.TaxIdentificationNumber,
        d.CitizenId,
        d.RegisteredFromDate,
        d.RegisteredToDate,
        d.IsActive,
        d.CreatedAt,
        d.UpdatedAt);

    public static DependentRegistrationRequestDto ToDto(RequestEntity r) => new(
        r.Id,
        r.EmployeeInformationId,
        r.EmployeeInformation?.FullName ?? string.Empty,
        r.RequestType,
        r.TargetDependentId,
        r.FullName,
        r.DateOfBirth,
        r.Gender,
        r.Relationship,
        r.TaxIdentificationNumber,
        r.CitizenId,
        r.RegisteredFromDate,
        r.RegisteredToDate,
        r.SupportingDocumentUrl,
        r.Status,
        r.Reason,
        r.ReviewNote,
        r.ReviewedByUserId,
        r.ReviewedAt,
        r.CancelledAt,
        r.CancelledByUserId,
        r.CancelReason,
        r.CreatedAt);
}
