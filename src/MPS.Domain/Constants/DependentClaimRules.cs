namespace MPS.Domain.Constants;

/// <summary>
/// Quy tắc "mỗi người phụ thuộc chỉ được MỘT người lao động giảm trừ" (Luật Thuế TNCN).
///
/// Guard cũ chỉ dò trùng CCCD TRONG CÙNG một nhân viên, nên hai nhân viên khai cùng một đứa trẻ
/// thì cả hai đơn đều được duyệt ⇒ giảm trừ gia cảnh bị tính hai lần cho cùng một người.
/// Thông báo gom về đây để tầng tạo đơn và tầng duyệt đơn nói CÙNG một câu.
/// </summary>
public static class DependentClaimRules
{
    /// <summary>Trùng với người phụ thuộc đã có của CHÍNH nhân viên đó.</summary>
    public static string DuplicateInSameEmployee(string citizenId) =>
        $"CCCD/CMND '{citizenId}' đã có trong danh sách người phụ thuộc đang hiệu lực của nhân viên này.";

    /// <summary>
    /// Trùng với người phụ thuộc mà NHÂN VIÊN KHÁC đang đăng ký.
    /// Cố ý KHÔNG nêu tên/mã nhân viên kia: người nộp đơn không cần (và không nên) biết đồng nghiệp
    /// nào đang kê khai; nhân sự tra được bằng chính số CCCD này.
    /// </summary>
    public static string ClaimedByAnotherEmployee(string citizenId) =>
        $"CCCD/CMND '{citizenId}' đang được một nhân viên khác đăng ký làm người phụ thuộc. "
        + "Mỗi người phụ thuộc chỉ được MỘT người lao động tính giảm trừ gia cảnh — "
        + "cần hủy đăng ký ở nhân viên kia trước.";
}
