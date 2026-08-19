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
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Repositories.Attendance;
using MPS.Infrastructure.Repositories.Compliance;
using MPS.Infrastructure.Repositories.Payroll;
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
        services.AddScoped<MPS.Application.Features.Attendance.Common.IAttendanceApproverResolver,
            MPS.Application.Features.Attendance.Common.AttendanceApproverResolver>();

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
        services.AddScoped<MPS.Application.Features.Evaluation.Common.IDirectEvaluationAutoGenerateService,
            MPS.Application.Features.Evaluation.Common.DirectEvaluationAutoGenerateService>();
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

        // ===== Payroll module (bảng lương hàng tháng) =====
        services.AddScoped<IPayrollFormulaSetRepository, PayrollFormulaSetRepository>();
        services.AddScoped<IPayrollPeriodRepository, PayrollPeriodRepository>();
        services.AddScoped<IPayrollAdjustmentRepository, PayrollAdjustmentRepository>();
        services.AddScoped<IPaymentObligationRepository, PaymentObligationRepository>();
        services.AddScoped<ICompanyPaymentSettingRepository,
            MPS.Infrastructure.Repositories.Payroll.CompanyPaymentSettingRepository>();
        services.AddScoped<MPS.Application.Features.Payroll.BhxhTaxPayment.Common.BhxhTaxEvidenceUploader>();
        services.AddScoped<MPS.Application.Features.Payroll.PaymentDocuments.Common.PaymentDocumentContextLoader>();
        services.AddScoped<IProductivityCoefficientRepository, ProductivityCoefficientRepository>();
        services.AddScoped<IPieceWorkSupportBandRepository, PieceWorkSupportBandRepository>();
        services.AddScoped<IPayrollDataCollector, PayrollDataCollector>();
        services.AddScoped<IPayrollReadinessChecker, PayrollReadinessChecker>();
        // BS-6: đánh cờ "cần tính lại" cho kỳ Draft khi nguồn (chấm công/đơn/phụ lục/đánh giá) đổi.
        services.AddScoped<IPayrollStaleMarker, PayrollStaleMarker>();
        // BS-5: capture chênh lệch THUCNHAN của NV khi kỳ Finalized bị sửa nguồn.
        services.AddScoped<IPayrollAdjustmentCapture, PayrollAdjustmentCapture>();
        // Gộp BS-6 + BS-5: 8 handler nguồn gọi qua đây, service tự phân nhánh Draft/Finalized.
        services.AddScoped<IPayrollSourceChangeHandler, PayrollSourceChangeHandler>();
        // #34/[D22]: tự cấp quỹ phép năm khi HĐ chuyển Active (hook trong ActivateContractCommandHandler).
        services.AddScoped<MPS.Application.Features.Attendance.LeaveBalance.Common.ILeaveBalanceInitializer,
            MPS.Application.Features.Attendance.LeaveBalance.Common.LeaveBalanceInitializer>();
        services.AddScoped<MPS.Application.Features.Attendance.LeaveBalance.Common.IAnnualLeaveGrantService,
            MPS.Application.Features.Attendance.LeaveBalance.Common.AnnualLeaveGrantService>();
        // Formula engine + calculator (stateless → singleton).
        services.AddSingleton<IFormulaFunctionRegistry, FormulaFunctionRegistry>();
        services.AddSingleton<IFormulaEvaluator, FormulaEvaluator>();
        services.AddSingleton<IColumnDependencyGraph, ColumnDependencyGraph>();
        services.AddSingleton<IPayrollCalculator, PayrollCalculator>();
        services.AddSingleton<IPayrollFieldCatalog, PayrollFieldCatalog>();
        services.AddSingleton<IPayrollFormulaValidator, PayrollFormulaValidator>();
        services.AddSingleton<MPS.Application.Common.Interfaces.Payroll.IPayrollExcelExporter,
            MPS.Infrastructure.Services.Payroll.PayrollExcelExporter>();

        // Chứng từ chi trả: biểu mẫu nhúng + bộ điền .xlsx có sẵn. Cả hai đều không giữ state.
        services.AddSingleton<MPS.Application.Common.Interfaces.Payroll.IPaymentTemplateProvider,
            MPS.Infrastructure.Services.Templates.EmbeddedPaymentTemplateProvider>();
        services.AddSingleton<MPS.Application.Common.Interfaces.Payroll.IXlsxTemplateFiller,
            MPS.Infrastructure.Services.Templates.XlsxTemplateFiller>();
        services.AddSingleton<IPayrollLawChangeNoteBuilder, PayrollLawChangeNoteBuilder>();

        // Attendance sheet export/import (Excel) — Exporter/Parser singleton (không state), Cache singleton.
        services.AddSingleton<MPS.Application.Common.Interfaces.Attendance.IAttendanceSheetExcelExporter,
            MPS.Infrastructure.Services.AttendanceSheet.AttendanceSheetExcelExporter>();
        services.AddSingleton<MPS.Application.Common.Interfaces.Attendance.IAttendanceSheetExcelParser,
            MPS.Infrastructure.Services.AttendanceSheet.AttendanceSheetExcelParser>();
        services.AddSingleton<MPS.Application.Common.Interfaces.Attendance.IAttendanceSheetImportCache,
            MPS.Infrastructure.Services.AttendanceSheet.AttendanceSheetImportCache>();
        services.AddScoped<MPS.Application.Features.Attendance.AttendanceSheet.Common.AttendanceSheetDataCollector>();

        // Attendance module — orchestrators
        services.AddScoped<MPS.Application.Features.Attendance.OvertimeRequest.Common.OvertimeCapChecker>();
        services.AddScoped<MPS.Application.Features.Attendance.LeaveRequest.Common.LeaveBalanceFallbackResolver>();
        services.AddScoped<MPS.Application.Features.Attendance.LateEarlyRequest.Common.LateEarlyLimitChecker>();
        // BS-8: trừ thật vào quỹ phép năm khi approver bật DeductFromLeaveBalance.
        services.AddScoped<MPS.Application.Features.Attendance.LateEarlyRequest.Common.LateEarlyLeaveDeduction>();
        // Đọc tổng số phút trễ/sớm đã Approved cho (NV, ngày) — dùng bởi calculator + recalc.
        services.AddScoped<MPS.Application.Features.Attendance.LateEarlyRequest.Common.LateEarlyExcuseResolver>();
        // Recompute giờ công cho bản ghi chấm công sau khi duyệt đơn đi trễ/về sớm.
        services.AddScoped<MPS.Application.Features.Attendance.LateEarlyRequest.Common.LateEarlyAttendanceDock>();
        services.AddScoped<MPS.Application.Features.Attendance.WfhRequest.Common.WfhQuotaChecker>();
        services.AddScoped<MPS.Application.Features.Attendance.LeaveRequest.Common.MedicalCertificateUploader>();
        services.AddScoped<MPS.Application.Features.Attendance.Common.AttendanceRequestDayConflictChecker>();
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
        services.AddSingleton<MPS.Application.Common.Services.IAttendanceStatusResolver,
            MPS.Application.Common.Services.AttendanceStatusResolver>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IScheduleTemplateRepository, ScheduleTemplateRepository>();
        services.AddScoped<MPS.Domain.Interfaces.Attendance.IFaceRegistrationRepository, MPS.Infrastructure.Repositories.Attendance.FaceRegistrationRepository>();
        services.AddScoped<IDayTypeResolver, DayTypeResolver>();
        services.AddScoped<MPS.Application.Common.Interfaces.IEmployeeDayOffChecker, EmployeeDayOffChecker>();
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
        // Email: handler inject IEmailSender = OutboxEmailSender (chỉ ghi outbox, không gửi).
        // Bên GỬI THẬT là IRealEmailSender (SMTP nếu cấu hình, ngược lại Logging) — worker dùng.
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        var smtpConfigured = !string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.SectionName}:Host"]);
        if (smtpConfigured)
            services.AddScoped<IRealEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IRealEmailSender, LoggingEmailSender>();

        services.AddScoped<IEmailSender, OutboxEmailSender>();
        services.AddScoped<IEmailOutboxRepository, EmailOutboxRepository>();

        // Mã hóa payload nhạy cảm (mật khẩu thô) lúc nằm ở Pending. Có key → AES-256-GCM;
        // không key (dev) → no-op (plaintext). Key sai định dạng → fail-fast tại startup.
        var payloadKey = configuration[$"{EmailDispatchOptions.SectionName}:EncryptionKey"];
        if (!string.IsNullOrWhiteSpace(payloadKey))
            services.AddSingleton<IOutboxPayloadProtector>(new AesOutboxPayloadProtector(payloadKey));
        else
            services.AddSingleton<IOutboxPayloadProtector, NoOpOutboxPayloadProtector>();

        services.Configure<EmailDispatchOptions>(configuration.GetSection(EmailDispatchOptions.SectionName));
        services.AddSingleton<EmailDispatchJob>();
        services.AddHostedService(sp => sp.GetRequiredService<EmailDispatchJob>());
        services.AddSingleton<EmailOutboxCleanupJob>();
        services.AddHostedService(sp => sp.GetRequiredService<EmailOutboxCleanupJob>());

        // Cross-cutting application services
        services.AddScoped<IEmployeeAccountProvisioner, EmployeeAccountProvisioner>();

        // Plan §2 N8 (Hướng B) — link đặt mật khẩu lần đầu thay cho gửi password thô.
        services.Configure<FirstLoginOptions>(configuration.GetSection(FirstLoginOptions.SectionName));
        services.AddScoped<IFirstLoginInvitationService, FirstLoginInvitationService>();

        // Link 1-lần trong mail "quên mật khẩu" (cùng cơ chế token với FirstLogin, hạn ngắn hơn).
        services.Configure<PasswordResetOptions>(configuration.GetSection(PasswordResetOptions.SectionName));

        // Document rendering (DOCX merge)
        services.AddScoped<IDocumentRenderingService, DocxMergeService>();

        // OCR CCCD — Claude vision là provider chính, FPT.AI giữ lại làm fallback.
        // Đăng ký 2 concrete type (mỗi cái một HttpClient riêng), rồi bọc bằng composite
        // để ICitizenIdOcrService vẫn là một service duy nhất với controller.
        services.AddOptions<AnthropicOcrOptions>()
            .Bind(configuration.GetSection(AnthropicOcrOptions.SectionName));
        services.AddOptions<FptAiOcrOptions>()
            .Bind(configuration.GetSection(FptAiOcrOptions.SectionName));
        services.AddHttpClient<AnthropicCitizenIdOcrService>();
        services.AddHttpClient<FptAiCitizenIdOcrService>();
        services.AddScoped<ICitizenIdOcrService, FallbackCitizenIdOcrService>();

        // AI attendance service — báo reload embeddings sau khi face registration thay đổi
        services.AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName));
        services.AddHttpClient<IFaceRecognitionSyncService, FaceRecognitionSyncService>();

        // Startup seeding
        services.AddScoped<MPS.Infrastructure.Services.PayrollDemoPeriodGenerator>();
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

        // Phụ lục Scheduled → Active khi tới EffectiveFrom (apply changes lên HĐ + trigger payroll).
        services.AddSingleton<AppendixEffectiveDateJob>();
        services.AddHostedService(sp => sp.GetRequiredService<AppendixEffectiveDateJob>());

        services.AddSingleton<TerminationAccountDeactivationJob>();
        services.AddHostedService(sp => sp.GetRequiredService<TerminationAccountDeactivationJob>());

        // [D22] — cấp quỹ phép năm cho toàn bộ NV vào đầu năm (tick ngày, chỉ chạy trong tháng 01).
        services.AddSingleton<AnnualLeaveGrantJob>();
        services.AddHostedService(sp => sp.GetRequiredService<AnnualLeaveGrantJob>());

        // Evaluation — sinh sẵn bảng chấm Direct (điểm mặc định) cho tổ trưởng/văn phòng, kỳ tháng trước.
        services.AddSingleton<DirectEvaluationAutoGenerateJob>();
        services.AddHostedService(sp => sp.GetRequiredService<DirectEvaluationAutoGenerateJob>());

        // Compliance module — background job
        services.AddSingleton<ComplianceActivationJob>();
        services.AddHostedService(sp => sp.GetRequiredService<ComplianceActivationJob>());

        // BHXH & Tax Payment Tracking — daily deadline scanner
        services.AddSingleton<BhxhTaxDeadlineJob>();
        services.AddHostedService(sp => sp.GetRequiredService<BhxhTaxDeadlineJob>());

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
