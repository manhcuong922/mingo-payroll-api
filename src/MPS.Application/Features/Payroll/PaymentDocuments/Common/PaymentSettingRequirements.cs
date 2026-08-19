using MPS.Domain.Entities.Payroll.Banking;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>
/// Những ô cấu hình chi trả BẮT BUỘC phải có trước khi lập được chứng từ.
///
/// <para>Dùng chung cho hai chỗ: màn hình xem trước (liệt kê ra để kế toán biết thiếu gì) và
/// lệnh xuất (chặn, không cho ra file khuyết ô). Tách khỏi handler để hai chỗ không bao giờ
/// lệch nhau — xem trước báo đủ mà xuất vẫn lỗi là tệ hơn không có xem trước.</para>
///
/// <para>Chỉ liệt kê ô mà thiếu là chứng từ SAI, không phải mọi ô của biểu mẫu. Tên chủ tài khoản,
/// mã CIF, điện thoại để trống thì bản in trống ô đó — ngân hàng vẫn nhận.</para>
/// </summary>
public static class PaymentSettingRequirements
{
    /// <summary>Lệnh chi lương (VPBank MB01). Thiếu mã ngân hàng chủ quản là không chia được 2 bảng.</summary>
    public static IReadOnlyList<string> ForSalaryOrder(CompanyPaymentSetting? setting)
    {
        if (setting is null) return new[] { "Toàn bộ cấu hình chi trả (chưa khai lần nào)" };

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(setting.SalaryBankCode))
            missing.Add("Ngân hàng chủ quản trả lương");
        if (string.IsNullOrWhiteSpace(setting.SalaryBankBranch))
            missing.Add("Chi nhánh nhận lệnh chi");
        if (string.IsNullOrWhiteSpace(setting.SalaryAccountNo))
            missing.Add("Tài khoản trích nợ chi lương");

        return missing;
    }

    /// <summary>Ủy nhiệm chi nộp BHXH (Agribank DP004).</summary>
    public static IReadOnlyList<string> ForBhxhRemittance(CompanyPaymentSetting? setting)
    {
        if (setting is null) return new[] { "Toàn bộ cấu hình chi trả (chưa khai lần nào)" };

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(setting.BhxhUnitCode))
            missing.Add("Mã đơn vị tham gia BHXH");
        if (string.IsNullOrWhiteSpace(setting.BhxhAgencyCode))
            missing.Add("Mã cơ quan BHXH quản lý thu");
        if (string.IsNullOrWhiteSpace(setting.BhxhAgencyName))
            missing.Add("Tên cơ quan BHXH thụ hưởng");
        if (string.IsNullOrWhiteSpace(setting.BhxhPayerAccountNo))
            missing.Add("Tài khoản công ty dùng để nộp BHXH");
        if (string.IsNullOrWhiteSpace(setting.BhxhPayerBankBranch))
            missing.Add("Ngân hàng/chi nhánh bên trả tiền");
        if (string.IsNullOrWhiteSpace(setting.BhxhBeneficiaryAccountNo))
            missing.Add("Tài khoản thụ hưởng của cơ quan BHXH");

        return missing;
    }
}
