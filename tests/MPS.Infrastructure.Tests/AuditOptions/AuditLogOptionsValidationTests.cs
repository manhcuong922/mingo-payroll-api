using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MPS.Application.Options;

namespace MPS.Infrastructure.Tests.AuditOptions;

/// <summary>
/// Verify rằng cấu hình invalid sẽ làm DI pipeline throw khi ValidateOnStart() chạy.
/// Chỉ test các rule của AuditLogOptions — không khởi tạo phần infra nặng khác.
/// </summary>
public sealed class AuditLogOptionsValidationTests
{
    private static IOptions<AuditLogOptions> ResolveOptions(Dictionary<string, string?> overrides)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["AuditLog:ActiveRetentionDays"] = "365",
            ["AuditLog:LowValueArchiveRetentionDays"] = "1825",
        };
        foreach (var kv in overrides)
            defaults[kv.Key] = kv.Value;

        var config = new ConfigurationBuilder().AddInMemoryCollection(defaults).Build();

        var services = new ServiceCollection();
        services.AddOptions<AuditLogOptions>()
            .Bind(config.GetSection(AuditLogOptions.SectionName))
            .Validate(o => o.ActiveRetentionDays is >= 90 and <= 3650,
                "ActiveRetentionDays out of range.")
            .Validate(o => o.LowValueArchiveRetentionDays >= o.ActiveRetentionDays
                           && o.LowValueArchiveRetentionDays <= 36500,
                "LowValueArchiveRetentionDays invalid.")
            .Validate(o => o.LowValueArchiveRetentionByModule
                .All(kv => !string.IsNullOrWhiteSpace(kv.Key)
                           && kv.Value >= o.ActiveRetentionDays
                           && kv.Value <= 36500),
                "LowValueArchiveRetentionByModule entries invalid.")
            .Validate(o => !o.HighValueArchiveRetentionDays.HasValue
                           || o.HighValueArchiveRetentionDays.Value >= o.LowValueArchiveRetentionDays,
                "HighValueArchiveRetentionDays invalid.");

        return services.BuildServiceProvider().GetRequiredService<IOptions<AuditLogOptions>>();
    }

    [Fact]
    public void Defaults_Are_Valid()
    {
        var act = () => _ = ResolveOptions(new()).Value;
        act.Should().NotThrow();
    }

    [Fact]
    public void Fails_When_ActiveRetention_Below_90()
    {
        var act = () => _ = ResolveOptions(new()
        {
            ["AuditLog:ActiveRetentionDays"] = "89",
        }).Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*ActiveRetentionDays*");
    }

    [Fact]
    public void Fails_When_ActiveRetention_Above_3650()
    {
        var act = () => _ = ResolveOptions(new()
        {
            ["AuditLog:ActiveRetentionDays"] = "3651",
        }).Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Fails_When_LowArchive_Less_Than_ActiveRetention()
    {
        var act = () => _ = ResolveOptions(new()
        {
            ["AuditLog:ActiveRetentionDays"] = "365",
            ["AuditLog:LowValueArchiveRetentionDays"] = "200",
        }).Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*LowValueArchiveRetentionDays*");
    }

    [Fact]
    public void Fails_When_PerModule_Override_Less_Than_ActiveRetention()
    {
        var act = () => _ = ResolveOptions(new()
        {
            ["AuditLog:ActiveRetentionDays"] = "365",
            ["AuditLog:LowValueArchiveRetentionByModule:Contract"] = "100",
        }).Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Fails_When_PerModule_Override_Exceeds_36500()
    {
        var act = () => _ = ResolveOptions(new()
        {
            ["AuditLog:LowValueArchiveRetentionByModule:Contract"] = "36501",
        }).Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Fails_When_HighRetention_Less_Than_LowRetention()
    {
        var act = () => _ = ResolveOptions(new()
        {
            ["AuditLog:LowValueArchiveRetentionDays"] = "1825",
            ["AuditLog:HighValueArchiveRetentionDays"] = "365",
        }).Value;

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*HighValueArchiveRetentionDays*");
    }

    [Fact]
    public void Passes_When_HighRetention_Null()
    {
        var act = () => _ = ResolveOptions(new()).Value;
        act.Should().NotThrow();
    }
}
