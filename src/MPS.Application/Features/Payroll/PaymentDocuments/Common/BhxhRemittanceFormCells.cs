namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>
/// Toạ độ ô cần ghi trên biểu mẫu ủy nhiệm chi Agribank (mẫu DP004).
///
/// <para>Sheet in <c>UNC</c> gồm 2 liên và chạy HOÀN TOÀN bằng công thức trỏ về sheet dữ liệu
/// <c>Nguon</c> — nên chỉ cần ghi 10 ô vô hướng dưới đây, không đụng gì tới bố cục bản in.</para>
///
/// <para>⚠️ Biểu mẫu nhúng và các hằng số này phải khớp nhau. Nếu thay biểu mẫu bằng bản khác
/// bố cục, <c>BhxhRemittanceTemplateShapeTests</c> sẽ đỏ trước khi ra chứng từ sai.</para>
/// </summary>
public static class BhxhRemittanceFormCells
{
    /// <summary>Sheet dữ liệu nguồn; sheet in tự lấy giá trị từ đây bằng công thức.</summary>
    public const string DataSheet = "Nguon";

    /// <summary>Sheet bản in (2 liên) — chỉ dùng để kiểm tra hình dạng biểu mẫu.</summary>
    public const string FormSheet = "UNC";

    public const string DocumentNo = "B11";
    public const string DocumentDate = "B12";

    /// <summary>Diễn giải — nơi đặt chuỗi cú pháp bắt buộc theo Công văn 1995/BHXH-TCKT.</summary>
    public const string Description = "B23";

    public const string AmountFigures = "B24";
    public const string AmountWords = "B25";

    public const string PayerName = "B38";
    public const string PayerAccountNo = "B39";
    public const string PayerBankName = "B40";

    public const string BeneficiaryName = "B42";
    public const string BeneficiaryAccountNo = "B43";
}
