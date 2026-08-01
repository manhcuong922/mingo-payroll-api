namespace MPS.Application.Features.Notifications.Recipients;

/// <summary>
/// Tra email của người nhận cho kênh gửi mail (Phase 1). Có 2 nguồn:
///   • <c>User.Email</c> — email công ty (mặc định).
///   • <c>EmployeeInformation.Email</c> — email cá nhân.
///
/// <paramref name="prefersPersonal"/> = <c>true</c> khi người nhận không còn login được nữa
/// (tài khoản khoá / nghỉ việc — N3 trong plan) — ưu tiên email cá nhân vì email công ty
/// thường đã bị thu hồi. Ngược lại ưu tiên email công ty.
///
/// Trả về <c>null</c> nếu không tra được email nào → dispatcher log và bỏ qua (không throw).
/// </summary>
public interface IUserEmailResolver
{
    Task<string?> ResolveDeliveryEmailAsync(
        int userId, bool prefersPersonal, CancellationToken ct = default);

    /// <summary>Tra hàng loạt user id → email theo cùng chính sách. Trả về map (userId → email);
    /// user không có email hợp lệ sẽ KHÔNG có trong map.</summary>
    Task<IReadOnlyDictionary<int, string>> ResolveDeliveryEmailsAsync(
        IReadOnlyCollection<int> userIds, bool prefersPersonal, CancellationToken ct = default);
}
