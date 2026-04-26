namespace MPS.API.Extensions;

/// <summary>
/// Ví dụ Extension Method - mở rộng cấu hình cho WebApplication.
/// </summary>
public static class ExampleServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        return services;
    }
}
