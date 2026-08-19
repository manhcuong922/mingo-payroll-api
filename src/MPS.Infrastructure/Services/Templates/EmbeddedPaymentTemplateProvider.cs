using System.Reflection;
using MPS.Application.Common.Interfaces.Payroll;

namespace MPS.Infrastructure.Services.Templates;

/// <summary>
/// Cấp biểu mẫu chứng từ chi trả từ embedded resource của assembly.
///
/// <para>Cố ý KHÔNG cho upload qua UI: code điền theo toạ độ ô cố định, một file sai bố cục
/// sẽ ghi số tiền vào ô khác mà không có cách nào phát hiện tự động.</para>
/// </summary>
public sealed class EmbeddedPaymentTemplateProvider : IPaymentTemplateProvider
{
    private const string ResourcePrefix = "MPS.Infrastructure.Resources.Templates.";

    private static readonly Assembly Owner = typeof(EmbeddedPaymentTemplateProvider).Assembly;

    public Stream Open(PaymentTemplateKind kind)
    {
        var fileName = kind switch
        {
            PaymentTemplateKind.SalaryPaymentOrder => "de-nghi-thanh-toan-luong-vpbank.xlsx",
            PaymentTemplateKind.BhxhRemittanceOrder => "uy-nhiem-chi-agribank.xlsx",
            PaymentTemplateKind.PitFinalization => "to-khai-qtt-tncn-02.docx",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Biểu mẫu chưa khai báo."),
        };

        return Owner.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidOperationException(
                $"PAYMENT_TEMPLATE_MISSING: không tìm thấy biểu mẫu nhúng '{fileName}'. " +
                "Kiểm tra mục EmbeddedResource trong MPS.Infrastructure.csproj.");
    }
}
