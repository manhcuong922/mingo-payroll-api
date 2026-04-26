namespace MPS.Infrastructure.ExternalServices;

/// <summary>
/// Ví dụ External Service - tích hợp dịch vụ bên thứ ba (email, payment...).
/// </summary>
public class ExampleEmailService
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        // TODO: Implement external email service integration
        return Task.CompletedTask;
    }
}
