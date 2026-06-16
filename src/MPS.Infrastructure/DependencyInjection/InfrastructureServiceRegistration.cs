using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Application.Common.Services;
using MPS.Application.Options;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.BackgroundJobs;
using MPS.Infrastructure.Email;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Repositories.Compliance;
using MPS.Infrastructure.Security;
using MPS.Infrastructure.Services;
using MPS.Infrastructure.Storage;

namespace MPS.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Repositories
        services.AddScoped<IPerformanceMetricRepository, PerformanceMetricRepository>();
        services.AddScoped<ITypePerformanceMetricRepository, TypePerformanceMetricRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEmployeeInformationRepository, EmployeeInformationRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

        // Contract module — catalog repositories
        services.AddScoped<ISalaryGradeRepository, SalaryGradeRepository>();
        services.AddScoped<IAllowanceTypeRepository, AllowanceTypeRepository>();
        services.AddScoped<IContractTemplateRepository, ContractTemplateRepository>();
        services.AddScoped<IContractPartyRepository, ContractPartyRepository>();

        // Contract module — core
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IContractLookupReader, ContractLookupReader>();
        services.AddScoped<IContractNoGenerator, ContractNoGenerator>();
        services.AddScoped<MPS.Application.Features.Contract.Common.ContractPreflightChecker>();

        // Contract module — appendix (Phase 7)
        services.AddScoped<IContractAppendixRepository, ContractAppendixRepository>();
        services.AddScoped<MPS.Application.Features.ContractAppendix.Common.AppendixApplier>();

        // Contract module — termination (Phase 8)
        services.AddScoped<IContractTerminationRepository, ContractTerminationRepository>();

        // Compliance module repositories
        services.AddScoped<ISocialInsuranceRateRepository, SocialInsuranceRateRepository>();
        services.AddScoped<IPitRegulationRepository, PitRegulationRepository>();
        services.AddScoped<IOtRateRepository, OtRateRepository>();
        services.AddScoped<IMinimumWageRepository, MinimumWageRepository>();
        services.AddScoped<IAllowanceExemptThresholdRepository, AllowanceExemptThresholdRepository>();
        services.AddScoped<ILaborLawConstantsRepository, LaborLawConstantsRepository>();

        // Compliance module services
        services.AddSingleton<ComplianceCacheInvalidator>();
        services.AddScoped<IComplianceLookupService, ComplianceLookupService>();

        // Compliance options
        services.AddOptions<ComplianceOptions>()
            .Bind(configuration.GetSection(ComplianceOptions.SectionName))
            .Validate(o => o.MaxForwardDays is >= 1 and <= 3650,
                "Compliance:MaxForwardDays must be between 1 and 3650.")
            .ValidateOnStart();

        // Auth & security
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        var smtpConfigured = !string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.SectionName}:Host"]);
        if (smtpConfigured)
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, LoggingEmailSender>();

        // Cross-cutting application services
        services.AddScoped<IEmployeeAccountProvisioner, EmployeeAccountProvisioner>();

        // Document rendering (DOCX merge)
        services.AddScoped<IDocumentRenderingService, DocxMergeService>();

        // OCR — FPT.AI citizen ID
        services.AddOptions<FptAiOcrOptions>()
            .Bind(configuration.GetSection(FptAiOcrOptions.SectionName));
        services.AddHttpClient<ICitizenIdOcrService, FptAiCitizenIdOcrService>();

        // Startup seeding
        services.AddScoped<DatabaseSeeder>();

        // Background jobs
        services.AddHostedService<RefreshTokenCleanupService>();

        // Contract module — Phase 9: clock + background jobs.
        // Đăng ký singleton trước rồi cùng instance dùng làm IHostedService → controller có thể
        // inject jobs để gọi RunOnceAsync manual.
        services.AddSingleton<MPS.Application.Common.Interfaces.IClock, MPS.Infrastructure.Time.SystemClock>();

        services.AddSingleton<ContractAutoConvertJob>();
        services.AddHostedService(sp => sp.GetRequiredService<ContractAutoConvertJob>());
        services.AddSingleton<ContractExpiryJob>();
        services.AddHostedService(sp => sp.GetRequiredService<ContractExpiryJob>());
        services.AddSingleton<ContractArchiveJob>();
        services.AddHostedService(sp => sp.GetRequiredService<ContractArchiveJob>());

        // Compliance module — background job
        services.AddSingleton<ComplianceActivationJob>();
        services.AddHostedService(sp => sp.GetRequiredService<ComplianceActivationJob>());

        // Storage (MinIO)
        services.AddOptions<MinioOptions>()
            .Bind(configuration.GetSection(MinioOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Endpoint),
                "Minio:Endpoint is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.AccessKey),
                "Minio:AccessKey is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey),
                "Minio:SecretKey is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.DefaultBucket),
                "Minio:DefaultBucket is required.")
            .ValidateOnStart();

        services.AddSingleton<IMinioClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            var client = new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey);
            if (options.UseSsl)
                client = client.WithSSL();
            return client.Build();
        });
        services.AddSingleton<IFileStorageService, MinioFileStorageService>();
        services.AddHostedService<MinioBucketInitializer>();

        return services;
    }
}
