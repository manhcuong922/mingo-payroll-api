using MediatR;
using MPS.Domain.Enums.Employee;

namespace MPS.Application.Features.EmployeeInformation.Commands;

public record EmployeeInformationUpdateCommand(
    int Id,
    int? UserId,
    string FullName,
    DateTime Dob,
    Gender Gender,
    string CitizenId,
    DateTime? CitizenIssuedDate,
    string? CitizenIssuedPlace,
    string PermanentAddress,
    string? ContactAddress,
    string? Email,
    string? PhoneNo,
    string? EducationLevel,
    int PositionId,
    int? DepartmentId,
    int DependentsNo,
    bool IsUnionMember,
    string? TaxNo,
    string? SocialNo,
    string? BankNumber,
    string? GuardianFullName,
    string? GuardianRelationship,
    string? GuardianPhoneNo,
    string? GuardianAddress) : IRequest<bool>;
