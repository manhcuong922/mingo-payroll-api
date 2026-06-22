namespace MPS.Infrastructure.ComplianceAutoUpdate.Ai;

/// <summary>
/// Prompt cho Claude vision phiên âm PDF scan/ảnh. Yêu cầu chép TRUNG THỰC toàn văn (không tóm tắt,
/// không bịa) và giữ cấu trúc "Điều N." + số liệu — để scope filter và grounding hoạt động đúng
/// trên bản phiên âm.
/// </summary>
public static class TranscriptionPrompt
{
    public const string System =
        "Bạn là công cụ OCR/phiên âm văn bản pháp luật Việt Nam. " +
        "Chép lại CHÍNH XÁC nội dung nhìn thấy trong tài liệu, KHÔNG tóm tắt, KHÔNG diễn giải, KHÔNG bịa. " +
        "Chỉ trả về phần text đã phiên âm, KHÔNG thêm lời dẫn hay markdown.";

    public const string User =
        "Phiên âm TOÀN BỘ nội dung văn bản trong tệp PDF này thành text thuần.\n" +
        "- Giữ nguyên cấu trúc điều khoản: mỗi điều bắt đầu bằng \"Điều N.\" trên một dòng riêng.\n" +
        "- Chép đầy đủ MỌI con số, tỷ lệ phần trăm, số tiền, mã vùng (Vùng I/II/III/IV) đúng như trong văn bản.\n" +
        "- Bảng biểu: đọc theo từng hàng, mỗi hàng một dòng, các ô cách nhau bằng khoảng trắng.\n" +
        "- Bỏ qua con dấu, chữ ký số, số trang, header/footer lặp lại.\n" +
        "Chỉ xuất phần text phiên âm.";
}
