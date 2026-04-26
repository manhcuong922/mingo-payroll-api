using Microsoft.Extensions.DependencyInjection;
using MPS.Domain.Interfaces;
using MPS.Application.Interfaces;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.DependencyInjection;

/// <summary>
/// Ví dụ DI Registration - đăng ký các dependency của tầng Infrastructure.
/// </summary>
public static class ExampleServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IExampleRepository, ExampleRepository>();
        services.AddScoped<IExampleService, ExampleService>();
        services.AddScoped<IPerformanceMetricRepository, PerformanceMetricRepository>();
        return services;
    }
}
