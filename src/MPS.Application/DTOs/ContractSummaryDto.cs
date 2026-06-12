namespace MPS.Application.DTOs;

public sealed record ContractSummaryDto(
    int Id,
    string ContractNo,
    string ContractCategory,
    string ContractSubtype,
    string Status,
    int EmployeeInformationId,
    string? EmployeeFullName,
    int EmployerPartyId,
    int PositionId,
    decimal SalaryAmountSnapshot,
    string SalaryCurrencySnapshot,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    DateTime? ArchivedAt);
