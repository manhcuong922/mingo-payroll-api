using MediatR;

namespace MPS.Application.Common.Models.Compliance;

/// <summary>
/// Sự kiện: một quy định/luật compliance vừa thay đổi (tạo/sửa/xoá bản nháp).
/// Đã publish sẵn ở các compliance command handler.
///
/// Các field bổ sung là TÙY CHỌN + có giá trị mặc định → mọi lời gọi cũ
/// <c>new ComplianceVersionChangedNotification(nameof(X))</c> vẫn biên dịch nguyên vẹn.
/// Khi handler nguồn tính được thay đổi số cụ thể, truyền thêm <paramref name="DisplayName"/> +
/// <paramref name="Changes"/> để thông báo hiển thị "từ A thành B".
/// </summary>
public sealed record ComplianceVersionChangedNotification(
    string ComplianceType,
    string? DisplayName = null,
    IReadOnlyList<ComplianceFieldChange>? Changes = null) : INotification;

/// <summary>Một thay đổi số/giá trị của luật: nhãn + giá trị cũ → mới (đã format sẵn thành chuỗi).</summary>
public sealed record ComplianceFieldChange(string Label, string OldValue, string NewValue);
