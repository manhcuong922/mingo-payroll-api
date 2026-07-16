using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Application.Common.Services;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;
using MPS.Application.Features.Compliance.AutoUpdate.Watcher;
using MPS.Application.Options;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.BackgroundJobs;
using MPS.Infrastructure.ComplianceAutoUpdate.Ai;
using MPS.Infrastructure.ComplianceAutoUpdate.Crawl;
using MPS.Infrastructure.ComplianceAutoUpdate.Crawl.Adapters;
using MPS.Infrastructure.ComplianceAutoUpdate.Intake;
using MPS.Infrastructure.ComplianceAutoUpdate.Pipeline;
using MPS.Infrastructure.ComplianceAutoUpdate.Validation;
using MPS.Infrastructure.Email;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Persistence.Interceptors;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Repositories.Attendance;
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
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<MPS.Application.Services.IDepartmentManagerAuthorizer,
            MPS.Application.Services.DepartmentManagerAuthorizer>();
        services.AddScoped<MPS.Application.Features.Attendance.Common.ApproverDepartmentGuard>();

        // Task/Production module
        services.AddScoped<IProcessTemplateRepository, ProcessTemplateRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();
        services.AddScoped<IProductionAttachmentRepository, ProductionAttachmentRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<MPS.Application.Services.ITaskGenerator, MPS.Application.Services.TaskGenerator>();
        services.AddScoped<MPS.Application.Services.IProgressService, MPS.Application.Services.ProgressService>();
        services.AddScoped<MPS.Application.Services.IProductionAttachmentService, MPS.Application.Services.ProductionAttachmentService>();
        // Evaluation module (chấm điểm nhân sự theo tháng)
        services.AddScoped<MPS.Domain.Interfaces.Evaluation.IOrderEvaluationRepository,
            MPS.Infrastructure.Repositories.Evaluation.OrderEvaluationRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Evaluation.IMonthlyEvaluationRepository,
            MPS.Infrastructure.Repositories.Evaluation.MonthlyEvaluationRepository>();
        services.AddScoped<MPS.Application.Common.Interfaces.IEvaluationCriteriaSnapshotService,
            MPS.Infrastructure.Repositories.Evaluation.EvaluationCriteriaSnapshotService>();
        services.AddScoped<MPS.Application.Features.Evaluation.Common.IMonthlyAggregationService,
            MPS.Application.Features.Evaluation.Common.MonthlyAggregationService>();
        services.AddScoped<MPS.Application.Common.Interfaces.IOrderEvaluationExcelService,
            MPS.Infrastructure.Services.OrderEvaluationExcelService>();

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

        // Company Policy module
        services.AddScoped<ICompanyPolicyRepository, CompanyPolicyRepository>();

        // Compliance module repositories
        services.AddScoped<ISocialInsuranceRateRepository, SocialInsuranceRateRepository>();
        services.AddScoped<IPitRegulationRepository, PitRegulationRepository>();
        services.AddScoped<IMinimumWageRepository, MinimumWageRepository>();
        services.AddScoped<IAllowanceExemptThresholdRepository, AllowanceExemptThresholdRepository>();
        services.AddScoped<ILaborLawConstantsRepository, LaborLawConstantsRepository>();
        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();

        // Compliance module services
        services.AddSingleton<ComplianceCacheInvalidator>();
        services.AddScoped<IComplianceLookupService, ComplianceLookupService>();

        // Attendance module — orchestrators
        services.AddScoped<MPS.Application.Features.Attendance.OvertimeRequest.Common.OvertimeCapChecker>();
        services.AddScoped<MPS.Application.Features.Attendance.LeaveRequest.Common.LeaveBalanceFallbackResolver>();
        services.AddScoped<MPS.Application.Features.Attendance.LateEarlyRequest.Common.LateEarlyLimitChecker>();
        services.AddScoped<MPS.Application.Features.Attendance.WfhRequest.Common.WfhQuotaChecker>();
        services.AddScoped<
            MPS.Application.Features.Attendance.OvertimeRequest.Common.IOvertimeMultiplierCalculator,
            MPS.Application.Features.Attendance.OvertimeRequest.Common.OvertimeMultiplierCalculator>();

        // Attendance module repositories (FE-ATT)
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IShiftTemplateRepository, ShiftTemplateRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IEmployeeShiftAssignmentRepository, EmployeeShiftAssignmentRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IPublicHolidayRepository, PublicHolidayRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IAttendanceRecordRepository, AttendanceRecordRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IAttendanceSettingRepository, AttendanceSettingRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IOvertimeRequestRepository, OvertimeRequestRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.ILeaveTypeRepository, LeaveTypeRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.ILeaveRequestRepository, LeaveRequestRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.ILeaveBalanceRepository, LeaveBalanceRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.ILateEarlyRequestRepository, LateEarlyRequestRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IWfhRequestRepository, WfhRequestRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Employee.IEmployeeDependentRepository, MPS.Infrastructure.Repositories.Employee.EmployeeDependentRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Employee.IDependentRegistrationRequestRepository, MPS.Infrastructure.Repositories.Employee.DependentRegistrationRequestRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IAttendanceAdjustmentRepository, AttendanceAdjustmentRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IAttendanceRuleRepository, AttendanceRuleRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IAttendanceRuleVersionRepository, AttendanceRuleVersionRepository>();
        services.AddScoped<MPS.Application.Common.Services.IAttendanceRateResolver,
            MPS.Application.Common.Services.AttendanceRateResolver>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IScheduleTemplateRepository, ScheduleTemplateRepository>();
        services.AddScoped<IDayTypeResolver, DayTypeResolver>();
        services.AddSingleton<MPS.Application.Common.Interfaces.IWorkHourSplitter, WorkHourSplitter>();
        services.AddScoped<MPS.Application.Common.Interfaces.IScheduleTemplateExpander, ScheduleTemplateExpander>();
        services.AddScoped<MPS.Application.Common.Interfaces.IRegularHoursCapChecker, RegularHoursCapChecker>();
        services.AddScoped<MPS.Application.Common.Interfaces.IAttendanceEditLockChecker, MPS.Application.Features.Attendance.AttendanceSetting.Common.AttendanceEditLockChecker>();
        services.AddSingleton<MPS.Application.Common.Interfaces.IMonthlyScheduleExcelParser, MonthlyScheduleExcelParser>();
        services.AddSingleton<MPS.Application.Common.Interfaces.IPublicHolidayExcelParser, PublicHolidayExcelParser>();
        services.AddSingleton<MPS.Application.Common.Interfaces.IMonthlyScheduleImportCache, MonthlyScheduleImportCache>();

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

        // ===== Compliance Auto-Update module (crawl + AI đề xuất cập nhật pháp luật) =====
        services.AddOptions<ComplianceAutoUpdateOptions>()
            .Bind(configuration.GetSection(ComplianceAutoUpdateOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ComplianceAutoUpdateOptions>>().Value);

        services.AddScoped<IComplianceUpdateProposalRepository, ComplianceUpdateProposalRepository>();
        services.AddScoped<IComplianceAuthoritativeSourceRepository, ComplianceAuthoritativeSourceRepository>();
        services.AddScoped<IComplianceLeadRepository, ComplianceLeadRepository>();
        services.AddScoped<IListFingerprintDetector, ListFingerprintDetector>();
        services.AddScoped<ICrawledDocumentTracker, CrawledDocumentTracker>();
        services.AddScoped<IContentScopeFilter, ContentScopeFilter>();
        services.AddScoped<ISourceGroundingValidator, SourceGroundingValidator>();
        services.AddScoped<IComplianceLogicValidator, ComplianceLogicValidator>();
        services.AddScoped<IComplianceDiffService, ComplianceDiffService>();
        services.AddScoped<IComplianceDocumentProcessor, ComplianceDocumentProcessor>();
        services.AddScoped<IDocumentTextFetcher, DocumentTextFetcher>();
        services.AddScoped<IComplianceCrawlPipeline, ComplianceCrawlPipeline>();

        // HttpClient riêng cho crawler (bật giải nén tự động) — dùng chung cho các adapter.
        services.AddHttpClient("compliance-crawler")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
            });
        services.AddScoped<ILegalSourceAdapter>(sp => new CongBaoAdapter(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("compliance-crawler"),
            sp.GetRequiredService<ComplianceAutoUpdateOptions>(),
            sp.GetRequiredService<ILogger<CongBaoAdapter>>()));
        services.AddScoped<ILegalSourceAdapter>(sp => new VbplAdapter(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("compliance-crawler"),
            sp.GetRequiredService<ComplianceAutoUpdateOptions>(),
            sp.GetRequiredService<ILogger<VbplAdapter>>()));

        // Extractor: có API key + provider=anthropic → Anthropic; ngược lại Offline (regex, không cần mạng).
        var llmKeyVar = configuration[$"{ComplianceAutoUpdateOptions.SectionName}:Llm:ApiKeyEnvVar"] ?? "OPERIX_LLM_API_KEY";
        var llmProvider = configuration[$"{ComplianceAutoUpdateOptions.SectionName}:Llm:Provider"] ?? "anthropic";
        var hasLlmKey = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(llmKeyVar));
        if (hasLlmKey && llmProvider.Equals("anthropic", StringComparison.OrdinalIgnoreCase))
        {
            // LLM/vision có thể chạy lâu (đặc biệt OCR PDF nhiều trang) → nới timeout HttpClient (mặc định 100s).
            var llmTimeout = TimeSpan.FromSeconds(300);
            services.AddHttpClient<AnthropicComplianceExtractor>(c => c.Timeout = llmTimeout);
            services.AddScoped<IComplianceExtractor>(sp => sp.GetRequiredService<AnthropicComplianceExtractor>());
            // OCR PDF scan/ảnh bằng vision — cùng điều kiện key như extractor.
            services.AddHttpClient<AnthropicVisionTranscriber>(c => c.Timeout = llmTimeout);
            services.AddScoped<IVisionTranscriber>(sp => sp.GetRequiredService<AnthropicVisionTranscriber>());
            // Kênh B — web search discoverer (cùng điều kiện key).
            services.AddHttpClient<AnthropicWebSearchDiscoverer>(c => c.Timeout = llmTimeout);
            services.AddScoped<IWebSearchDiscoverer>(sp => sp.GetRequiredService<AnthropicWebSearchDiscoverer>());
        }
        else
        {
            services.AddScoped<IComplianceExtractor, OfflineComplianceExtractor>();
            services.AddScoped<IVisionTranscriber, OfflineVisionTranscriber>();
            services.AddScoped<IWebSearchDiscoverer, OfflineWebSearchDiscoverer>();
        }

        // Kênh B — watcher chủ động + job định kỳ ("Dò ngay" + chạy nền).
        services.AddScoped<IComplianceWatcher, ComplianceWatcher>();
        services.AddSingleton<ComplianceWatcherJob>();
        services.AddHostedService(sp => sp.GetRequiredService<ComplianceWatcherJob>());

        // Runner — đợt này CHỈ singleton, chạy thủ công qua endpoint. Chuyển background: xem chú thích trong class.
        services.AddSingleton<ComplianceAutoUpdateJob>();

        // Áp proposal đã duyệt → tạo version compliance mới (tái dùng Create command sẵn có).
        services.AddScoped<MPS.Application.Features.Compliance.AutoUpdate.Approval.ComplianceProposalApplier>();

        // Audit log module (FT-39)
        services.AddOptions<AuditLogOptions>()
            .Bind(configuration.GetSection(AuditLogOptions.SectionName))
            .Validate(o => o.ActiveRetentionDays is >= 90 and <= 3650,
                "AuditLog:ActiveRetentionDays must be between 90 and 3650.")
            .Validate(o => o.LowValueArchiveRetentionDays >= o.ActiveRetentionDays
                           && o.LowValueArchiveRetentionDays <= 36500,
                "AuditLog:LowValueArchiveRetentionDays must be ≥ ActiveRetentionDays and ≤ 36500.")
            .Validate(o => o.LowValueArchiveRetentionByModule
                .All(kv => !string.IsNullOrWhiteSpace(kv.Key)
                           && kv.Value >= o.ActiveRetentionDays
                           && kv.Value <= 36500),
                "AuditLog:LowValueArchiveRetentionByModule entries must have non-empty keys and values ≥ ActiveRetentionDays and ≤ 36500.")
            .Validate(o => !o.HighValueArchiveRetentionDays.HasValue
                           || o.HighValueArchiveRetentionDays.Value >= o.LowValueArchiveRetentionDays,
                "AuditLog:HighValueArchiveRetentionDays, if set, must be ≥ LowValueArchiveRetentionDays.")
            .ValidateOnStart();

        services.AddSingleton<IAuditSeverityClassifier, DefaultAuditSeverityClassifier>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddSingleton<AuditLogRetentionJob>();
        services.AddHostedService(sp => sp.GetRequiredService<AuditLogRetentionJob>());

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
