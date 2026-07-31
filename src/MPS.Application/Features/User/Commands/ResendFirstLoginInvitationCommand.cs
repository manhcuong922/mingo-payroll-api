using MediatR;

namespace MPS.Application.Features.User.Commands;

/// <summary>
/// HR bấm "Gửi lại link đặt mật khẩu" trên trang quản lý user — vd user không tìm thấy mail cũ,
/// hoặc link 72h đã hết hạn. Trả về thời điểm token mới hết hạn (UTC).
/// </summary>
public sealed record ResendFirstLoginInvitationCommand(int UserId) : IRequest<DateTime>;
