namespace MPS.Application.DTOs;

public record EmployeeInformationDto(
    int Id,
    int UserId,
    string EmployeeCode,
    string FullName,
    string TaxNo,
    string SocialNo,
    string BankNumber,
    int PositionId,
    string PositionName,
    int DependentsNo,
    string CitizenId,
    string Address);
