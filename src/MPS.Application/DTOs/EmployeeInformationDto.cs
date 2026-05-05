namespace MPS.Application.DTOs;

public record EmployeeInformationDto(
    int Id,
    string EmployeeCode,
    string TaxNo,
    string SocialNo,
    string BankNumber,
    int PositionId,
    string PositionName,
    int DependentsNo,
    string CitizenId,
    string Address);