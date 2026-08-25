namespace MPS.Application.Common.Interfaces.Payroll;

/// <summary>Biểu mẫu chính thức dùng để sinh chứng từ chi trả.</summary>
public enum PaymentTemplateKind
{
    /// <summary>Đề nghị thanh toán lương kiêm ủy nhiệm chi — VPBank, mẫu MB01.QT-NVTT/07 (.xlsx).</summary>
    SalaryPaymentOrder = 1,

    /// <summary>Ủy nhiệm chi nộp BHXH/BHYT/BHTN — Agribank, mẫu DP004 (.xlsx).</summary>
    BhxhRemittanceOrder = 2,

    /// <summary>Tờ khai quyết toán thuế TNCN mẫu 02/QTT-TNCN (.docx).</summary>
    PitFinalization = 3,
}

/// <summary>Mở luồng đọc biểu mẫu gốc. Luồng trả về do bên gọi chịu trách nhiệm giải phóng.</summary>
public interface IPaymentTemplateProvider
{
    Stream Open(PaymentTemplateKind kind);
}
