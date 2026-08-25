namespace MPS.Application.Features.Payroll.PaymentDocuments;

/// <summary>Một NV bị loại khỏi lệnh chi lương, kèm lý do để kế toán xử lý tay.</summary>
public sealed record SalaryPaymentExclusionDto(
    string EmployeeCode,
    string FullName,
    decimal Amount,
    string Reason);

/// <summary>
/// Xem trước lệnh chi lương: ai vào bảng nào, tổng bao nhiêu, ai rớt và vì sao.
///
/// <para>Có màn hình này thì kế toán biết TRƯỚC khi tải file rằng còn 3 người thiếu chi nhánh
/// ngân hàng — thay vì phát hiện lúc VPBank trả lại cả lệnh chi.</para>
/// </summary>
public sealed record SalaryPaymentPreviewDto(
    IReadOnlyList<string> MissingSettings,
    string? SalaryBankCode,
    string? SalaryBankName,
    int InHouseCount,
    decimal InHouseAmount,
    int OtherBanksCount,
    decimal OtherBanksAmount,
    int TotalCount,
    decimal TotalAmount,
    IReadOnlyList<SalaryPaymentExclusionDto> Excluded)
{
    /// <summary>Xuất được khi cấu hình đủ và còn ít nhất 1 người để chuyển tiền.</summary>
    public bool CanExport => MissingSettings.Count == 0 && TotalCount > 0;
}

/// <summary>Xem trước ủy nhiệm chi nộp BHXH của kỳ.</summary>
/// <param name="RemittanceContent">
/// Nội dung chuyển khoản theo Công văn 1995/BHXH-TCKT — hiển thị để kế toán đối chiếu bằng mắt
/// trước khi ra chứng từ; null khi mã đơn vị/mã cơ quan chưa hợp lệ.
/// </param>
public sealed record BhxhRemittancePreviewDto(
    IReadOnlyList<string> MissingSettings,
    decimal EmployeeContribution,
    decimal EmployerContribution,
    decimal TotalAmount,
    string? RemittanceContent,
    string? ContentError)
{
    public bool CanExport => MissingSettings.Count == 0 && ContentError is null && TotalAmount > 0;
}

/// <summary>Gộp phần xem trước của 2 chứng từ lập theo KỲ (tờ khai thuế lập theo NĂM, tách riêng).</summary>
public sealed record PeriodPaymentDocumentsPreviewDto(
    int PeriodId,
    int PeriodYear,
    int PeriodMonth,
    string PeriodStatus,
    SalaryPaymentPreviewDto SalaryOrder,
    BhxhRemittancePreviewDto BhxhRemittance);

/// <summary>Một NV trên bảng xem trước quyết toán thuế TNCN năm.</summary>
/// <param name="AnnualTax">Thuế tính lại theo biểu NĂM (chỉ tiêu [37]).</param>
/// <param name="WithheldTax">Tổng thuế đã khấu trừ 12 tháng (chỉ tiêu [39]).</param>
/// <param name="Difference">
/// Dương = còn phải nộp thêm, âm = nộp thừa (được hoàn). Đây chính là con số kế toán cần nhìn.
/// </param>
public sealed record PitFinalizationEmployeeDto(
    int EmployeeInformationId,
    string EmployeeCode,
    string FullName,
    int MonthsCount,
    decimal TaxableIncome,
    decimal AnnualTax,
    decimal WithheldTax,
    decimal Difference);

/// <summary>Xem trước bộ tờ khai 02/QTT-TNCN của một năm.</summary>
public sealed record PitFinalizationPreviewDto(
    int Year,
    IReadOnlyList<string> MissingSettings,
    IReadOnlyList<PitFinalizationEmployeeDto> Employees)
{
    public bool CanExport => MissingSettings.Count == 0 && Employees.Count > 0;
}
