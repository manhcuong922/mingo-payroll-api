namespace MPS.Application.Common.Interfaces;

public interface IEmployeeAccountProvisioner
{
    Task<EmployeeAccountCredentials> ProvisionAsync(
        int employeeInformationId,
        int roleId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Kết quả cấp tài khoản. Sau plan §2 N8 (Hướng B), <b>không còn</b> trả password thô —
/// user tự đặt password qua link mời <see cref="IFirstLoginInvitationService"/>.
/// Provisioner vẫn hash 1 random password vào DB để user không có "empty password" hợp lệ;
/// user không được biết password random đó và sẽ overwrite nó khi click link.
/// </summary>
public sealed record EmployeeAccountCredentials(
    int UserId,
    string EmployeeCode,
    string Username);
