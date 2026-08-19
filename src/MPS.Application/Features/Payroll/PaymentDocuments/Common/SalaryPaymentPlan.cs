namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>Tên và địa chỉ pháp nhân in trên đầu chứng từ, lấy từ <c>ContractParty</c>.</summary>
public sealed record SalaryPaymentCompanyProfile(string LegalName, string Address);

/// <summary>Một người hưởng lương trên lệnh chi.</summary>
public sealed record SalaryPaymentBeneficiary(
    string EmployeeCode,
    string FullName,
    string AccountNo,
    decimal Amount,
    string? Address,
    string BankCode,
    string? BankBranch,
    string? ProvinceFormName);

/// <summary>Lý do một NV bị loại khỏi lệnh chi — phải báo cho kế toán xử lý tay.</summary>
public sealed record SalaryPaymentExclusion(
    string EmployeeCode,
    string FullName,
    decimal Amount,
    string Reason);

/// <summary>
/// Kết quả tính toán cho lệnh chi lương — thuần dữ liệu, không dính OpenXml, nên toàn bộ
/// luật nghiệp vụ (ai vào bảng nào, ai bị loại, tổng bao nhiêu) test được mà không cần mở Excel.
/// </summary>
public sealed record SalaryPaymentPlan(
    IReadOnlyList<SalaryPaymentBeneficiary> InHouseBank,
    IReadOnlyList<SalaryPaymentBeneficiary> OtherBanks,
    IReadOnlyList<SalaryPaymentExclusion> Excluded)
{
    public int TotalCount => InHouseBank.Count + OtherBanks.Count;

    public decimal TotalAmount => InHouseBank.Sum(b => b.Amount) + OtherBanks.Sum(b => b.Amount);

    /// <summary>Số dòng cần chèn thêm cho bảng 1 (ngoài các dòng biểu mẫu kẻ sẵn).</summary>
    public int ExtraRowsForInHouse =>
        Math.Max(0, InHouseBank.Count - SalaryPaymentFormCells.PrebakedRowsPerTable);

    /// <summary>Số dòng cần chèn thêm cho bảng 2.</summary>
    public int ExtraRowsForOthers =>
        Math.Max(0, OtherBanks.Count - SalaryPaymentFormCells.PrebakedRowsPerTable);
}
