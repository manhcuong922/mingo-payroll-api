using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.Tests.DependencyInjection;

/// <summary>
/// Chốt phần đăng ký DI của OCR CCCD: composite phải resolve được cùng 2 provider bên dưới.
/// Unit test kia stub HttpClient nên không bắt được lỗi đăng ký thiếu/sai lifetime.
/// </summary>
public sealed class CitizenIdOcrRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AnthropicOcrOptions.SectionName}:BaseUrl"] = "http://localhost:8317",
                [$"{AnthropicOcrOptions.SectionName}:Model"] = "claude-opus-5",
                [$"{FptAiOcrOptions.SectionName}:ApiKey"] = "fpt-key",
                [$"{FptAiOcrOptions.SectionName}:BaseUrl"] = "https://api.fpt.ai",
            })
            .Build();

        var services = new ServiceCollection();

        // Đăng ký đúng khối OCR trong AddInfrastructure — không kéo cả DbContext/MinIO vào test.
        services.AddOptions<AnthropicOcrOptions>()
            .Bind(configuration.GetSection(AnthropicOcrOptions.SectionName));
        services.AddOptions<FptAiOcrOptions>()
            .Bind(configuration.GetSection(FptAiOcrOptions.SectionName));
        services.AddHttpClient<AnthropicCitizenIdOcrService>();
        services.AddHttpClient<FptAiCitizenIdOcrService>();
        services.AddScoped<ICitizenIdOcrService, FallbackCitizenIdOcrService>();
        services.AddLogging();

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void Resolves_Composite_As_The_Registered_Implementation()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ICitizenIdOcrService>()
            .Should().BeOfType<FallbackCitizenIdOcrService>();
    }

    [Fact]
    public void Resolves_Both_Providers_With_Their_Own_HttpClient()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var anthropic = scope.ServiceProvider.GetRequiredService<AnthropicCitizenIdOcrService>();
        var fpt = scope.ServiceProvider.GetRequiredService<FptAiCitizenIdOcrService>();

        anthropic.Should().NotBeNull();
        fpt.IsConfigured.Should().BeTrue("key FPT có trong config nên nhánh fallback phải sẵn sàng");
    }

    /// <summary>Options phải bind từ section đúng tên — sai tên section thì service im lặng chạy sai endpoint.</summary>
    [Fact]
    public void Binds_Options_From_Expected_Config_Sections()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var anthropic = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOcrOptions>>().Value;

        anthropic.BaseUrl.Should().Be("http://localhost:8317");
        anthropic.Model.Should().Be("claude-opus-5");
        anthropic.ApiKeyEnvVar.Should().Be("OPERIX_LLM_API_KEY", "mặc định phải giữ khi config không override");
    }
}
