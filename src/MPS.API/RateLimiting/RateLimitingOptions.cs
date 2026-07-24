namespace MPS.API.RateLimiting;

/// <summary>
/// Hạn mức tần suất (rate limit) cho các nhóm endpoint nhạy cảm. Bind từ section "RateLimiting".
///
/// Giá trị mặc định ở đây là mức PRODUCTION; môi trường khác chỉ cần override trong appsettings
/// tương ứng, không phải sửa code. Trước đây các con số này nằm rải rác dưới dạng magic number
/// trong Program.cs nên không thể điều chỉnh theo môi trường.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Số lần đăng nhập tối đa mỗi phút cho MỘT tài khoản (phân vùng theo username + IP).
    /// Đây là lớp chống dò mật khẩu: kẻ tấn công nhắm vào 1 tài khoản chỉ được 5 lượt/phút.
    /// </summary>
    public int LoginPerAccountPerMinute { get; init; } = 5;

    /// <summary>
    /// Trần đăng nhập mỗi phút cho MỘT địa chỉ IP (mọi tài khoản cộng lại).
    /// Chống dò trải rộng (đổi username liên tục để né hạn mức theo tài khoản), đồng thời đủ rộng
    /// để cả văn phòng dùng chung một IP NAT vẫn đăng nhập bình thường đầu ca.
    /// </summary>
    public int LoginPerIpPerMinute { get; init; } = 60;

    /// <summary>Số lần làm mới token mỗi phút cho một IP.</summary>
    public int RefreshPerIpPerMinute { get; init; } = 20;

    /// <summary>
    /// Số lượt tải file hợp đồng (kích hoạt / thay thế / phụ lục / thanh lý) mỗi phút cho MỘT người dùng.
    /// Nhân sự thường onboard theo lô nên mức quá thấp sẽ chặn nghiệp vụ bình thường.
    /// </summary>
    public int ContractUploadPerUserPerMinute { get; init; } = 20;

    /// <summary>Số lượt gọi OCR căn cước mỗi phút cho MỘT người dùng.</summary>
    public int OcrPerUserPerMinute { get; init; } = 10;
}
