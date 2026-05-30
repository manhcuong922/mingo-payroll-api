namespace MPS.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string toEmail, string resetToken, DateTime expiresAt, CancellationToken cancellationToken = default);
}
