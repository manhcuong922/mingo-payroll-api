namespace MPS.Domain.Constants;

/// <summary>
/// Khuôn dạng các định danh cá nhân theo quy định Việt Nam — đặt ở Domain để FE/BE và mọi
/// validator (hồ sơ NV, người phụ thuộc, tài khoản) dùng CHUNG một chuẩn, tránh mỗi nơi một kiểu.
/// </summary>
public static class VietnamIdentifierFormats
{
    /// <summary>
    /// Số điện thoại di động Việt Nam: bắt đầu bằng <c>0</c> hoặc <c>+84</c>, tiếp theo là đầu số
    /// nhà mạng (3, 5, 7, 8, 9 — theo quy hoạch kho số của Bộ TT&amp;TT sau chuyển đổi 11→10 số),
    /// rồi 8 chữ số. Ví dụ hợp lệ: <c>0912345678</c>, <c>+84912345678</c>.
    /// </summary>
    public const string PhoneNumberPattern = @"^(?:0|\+84)(?:3|5|7|8|9)\d{8}$";

    public const string PhoneNumberMessage =
        "Số điện thoại không hợp lệ. Định dạng: 10 chữ số bắt đầu bằng 0 (hoặc +84), đầu số 3/5/7/8/9 — ví dụ 0912345678.";

    /// <summary>
    /// Số CCCD: đúng 12 chữ số (Điều 3 Thông tư 07/2016/TT-BCA và Luật Căn cước 2023).
    /// CMND 9 số đã hết hiệu lực từ 2025-01-01 nên không còn nhận.
    /// </summary>
    public const string CitizenIdPattern = @"^\d{12}$";

    public const string CitizenIdMessage = "CCCD phải đúng 12 chữ số.";
}
