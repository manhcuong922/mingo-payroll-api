namespace MPS.Application.Features.Payroll.CompanyPaymentSettings;

/// <summary>Một ngân hàng trong danh mục VPBank phát hành kèm mẫu MB01.</summary>
public sealed record BankCatalogDto(string Code, string FullName, bool IsForeign);

/// <summary>Một tỉnh/TP trong danh mục của lệnh chi.</summary>
/// <param name="FormName">Tên không dấu VIẾT HOA — đúng thứ được ghi lên lệnh chi.</param>
public sealed record ProvinceCatalogDto(string Code, string Name, string FormName);

/// <summary>
/// Hai danh mục dùng để chọn ngân hàng/tỉnh, trả cùng một lượt.
///
/// <para>Gộp làm một endpoint vì cả form hồ sơ nhân sự lẫn màn hình cấu hình chi trả đều cần đủ
/// cả hai; tách đôi chỉ tạo thêm một vòng request mà không ai dùng riêng lẻ.</para>
/// </summary>
public sealed record BankingCatalogsDto(
    IReadOnlyList<BankCatalogDto> Banks,
    IReadOnlyList<ProvinceCatalogDto> Provinces);

/// <summary>
/// Cấu hình chi trả cấp công ty, kèm sẵn phần chẩn đoán để màn hình cấu hình chỉ ra được
/// ô nào còn thiếu cho từng chứng từ thay vì bắt kế toán đoán.
/// </summary>
/// <param name="RemittanceContentPreview">
/// Nội dung chuyển khoản BHXH dựng từ mã đơn vị + mã cơ quan hiện tại; null khi chưa khai đủ
/// hoặc mã sai cú pháp (xem <paramref name="RemittanceContentError"/>).
/// </param>
public sealed record CompanyPaymentSettingDto(
    string? SalaryBankCode,
    string? SalaryBankName,
    string? SalaryBankBranch,
    string? SalaryAccountNo,
    string? SalaryAccountName,
    string? SalaryCustomerCif,
    string? SalaryContactPhone,
    string? BhxhUnitCode,
    string? BhxhAgencyCode,
    string? BhxhAgencyName,
    string? BhxhPayerBankCode,
    string? BhxhPayerBankName,
    string? BhxhPayerBankBranch,
    string? BhxhPayerAccountNo,
    string? BhxhBeneficiaryAccountNo,
    string? BhxhBeneficiaryBankName,
    string? RemittanceContentPreview,
    string? RemittanceContentError,
    IReadOnlyList<string> MissingForSalaryOrder,
    IReadOnlyList<string> MissingForBhxhRemittance,
    DateTime? UpdatedAt);
