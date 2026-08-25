namespace MPS.API.Requests.Payroll;

/// <summary>
/// Body cho PUT cấu hình chi trả. Full-replace: field không gửi (null) đồng nghĩa xoá trắng ô đó
/// — màn hình cấu hình luôn gửi cả cụm.
/// </summary>
public sealed record UpdateCompanyPaymentSettingRequest(
    string? SalaryBankCode,
    string? SalaryBankBranch,
    string? SalaryAccountNo,
    string? SalaryAccountName,
    string? SalaryCustomerCif,
    string? SalaryContactPhone,
    string? BhxhUnitCode,
    string? BhxhAgencyCode,
    string? BhxhAgencyName,
    string? BhxhPayerBankCode,
    string? BhxhPayerBankBranch,
    string? BhxhPayerAccountNo,
    string? BhxhBeneficiaryAccountNo,
    string? BhxhBeneficiaryBankName);
