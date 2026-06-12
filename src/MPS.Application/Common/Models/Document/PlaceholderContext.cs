namespace MPS.Application.Common.Models.Document;

/// <summary>
/// Giá trị đã format sẵn (string) cho từng placeholder {{key}} trong template DOCX.
/// Key dùng cú pháp phẳng camelCase, ví dụ {{contractNo}}, {{employeeFullName}}.
/// </summary>
public sealed record PlaceholderContext(
    // Hợp đồng
    string ContractNo,
    string ContractCategory,
    string EffectiveFrom,
    string EffectiveTo,
    string SignedDate,
    string SignedPlace,

    // Bên A — Pháp nhân (NSDLĐ)
    string EmployerLegalName,
    string EmployerTaxCode,
    string EmployerAddress,
    string EmployerRepName,
    string EmployerRepTitle,

    // Bên B — Người lao động (NLĐ)
    string EmployeeFullName,
    string EmployeeDob,
    string EmployeeGender,
    string EmployeeCitizenId,
    string EmployeeCitizenIssuedDate,
    string EmployeeCitizenIssuedPlace,
    string EmployeePermanentAddress,
    string EmployeeContactAddress,

    // Công việc
    string PositionName,
    string JobDescription,
    string WorkLocation,

    // Lương
    string SalaryAmount,
    string SalaryCurrency,
    string SalaryPayCycle,

    // Phụ cấp
    string Allowances
)
{
    public IReadOnlyDictionary<string, string> ToDictionary() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["contractNo"]                 = ContractNo,
        ["contractCategory"]           = ContractCategory,
        ["effectiveFrom"]              = EffectiveFrom,
        ["effectiveTo"]                = EffectiveTo,
        ["signedDate"]                 = SignedDate,
        ["signedPlace"]                = SignedPlace,
        ["employerLegalName"]          = EmployerLegalName,
        ["employerTaxCode"]            = EmployerTaxCode,
        ["employerAddress"]            = EmployerAddress,
        ["employerRepName"]            = EmployerRepName,
        ["employerRepTitle"]           = EmployerRepTitle,
        ["employeeFullName"]           = EmployeeFullName,
        ["employeeDob"]                = EmployeeDob,
        ["employeeGender"]             = EmployeeGender,
        ["employeeCitizenId"]          = EmployeeCitizenId,
        ["employeeCitizenIssuedDate"]  = EmployeeCitizenIssuedDate,
        ["employeeCitizenIssuedPlace"] = EmployeeCitizenIssuedPlace,
        ["employeePermanentAddress"]   = EmployeePermanentAddress,
        ["employeeContactAddress"]     = EmployeeContactAddress,
        ["positionName"]               = PositionName,
        ["jobDescription"]             = JobDescription,
        ["workLocation"]               = WorkLocation,
        ["salaryAmount"]               = SalaryAmount,
        ["salaryCurrency"]             = SalaryCurrency,
        ["salaryPayCycle"]             = SalaryPayCycle,
        ["allowances"]                 = Allowances,
    };
}
