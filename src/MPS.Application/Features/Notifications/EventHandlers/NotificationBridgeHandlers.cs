using MediatR;
using MPS.Application.Common.Models.Attendance;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Models.Evaluation;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Common.Models.Payroll;
using MPS.Application.Features.Notifications.Abstractions;

namespace MPS.Application.Features.Notifications.EventHandlers;

// Các handler mỏng nối domain event (MediatR) → INotifier. MediatR tự phát hiện & gọi khi event publish.
// Notifier tự bọc try/catch nên các handler này KHÔNG làm hỏng nghiệp vụ nguồn nếu phát thông báo lỗi.
//
// Thêm loại thông báo mới về sau = thêm 1 handler mỏng như dưới đây + 1 template tương ứng.

/// <summary>Luật/quy định thay đổi → phát thông báo (template ComplianceChangedTemplate xử lý).</summary>
public sealed class ComplianceChangedBridge : INotificationHandler<ComplianceVersionChangedNotification>
{
    private readonly INotifier _notifier;
    public ComplianceChangedBridge(INotifier notifier) => _notifier = notifier;

    public Task Handle(ComplianceVersionChangedNotification n, CancellationToken ct) =>
        _notifier.RaiseAsync(n, ct);
}

/// <summary>Nội quy công ty thay đổi → phát thông báo.</summary>
public sealed class CompanyPolicyChangedBridge : INotificationHandler<CompanyPolicyChangedNotification>
{
    private readonly INotifier _notifier;
    public CompanyPolicyChangedBridge(INotifier notifier) => _notifier = notifier;

    public Task Handle(CompanyPolicyChangedNotification n, CancellationToken ct) =>
        _notifier.RaiseAsync(n, ct);
}

/// <summary>Giao đơn/việc cho nhân sự → phát thông báo cho người được giao.</summary>
public sealed class TaskAssignedBridge : INotificationHandler<TaskAssignedDomainNotification>
{
    private readonly INotifier _notifier;
    public TaskAssignedBridge(INotifier notifier) => _notifier = notifier;

    public Task Handle(TaskAssignedDomainNotification n, CancellationToken ct) =>
        _notifier.RaiseAsync(n, ct);
}

// ===== Phase 3 — Đơn từ =====

public sealed class AttendanceRequestSubmittedBridge : INotificationHandler<AttendanceRequestSubmittedNotification>
{
    private readonly INotifier _notifier;
    public AttendanceRequestSubmittedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(AttendanceRequestSubmittedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class AttendanceRequestDecidedBridge : INotificationHandler<AttendanceRequestDecidedNotification>
{
    private readonly INotifier _notifier;
    public AttendanceRequestDecidedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(AttendanceRequestDecidedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

// ===== Phase 4 — Payroll / BHXH / Thuế =====

/// <summary>Kỳ lương khoá → thông báo Kế toán/HR + PayslipReady cho từng NV có payslip (§N11).</summary>
public sealed class PayrollPeriodLockedBridge : INotificationHandler<PayrollPeriodLockedNotification>
{
    private readonly INotifier _notifier;
    public PayrollPeriodLockedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(PayrollPeriodLockedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class PayslipReadyBridge : INotificationHandler<PayslipReadyNotification>
{
    private readonly INotifier _notifier;
    public PayslipReadyBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(PayslipReadyNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class BhxhTaxDeadlineSoonBridge : INotificationHandler<BhxhTaxDeadlineSoonNotification>
{
    private readonly INotifier _notifier;
    public BhxhTaxDeadlineSoonBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(BhxhTaxDeadlineSoonNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class BhxhTaxOverdueBridge : INotificationHandler<BhxhTaxOverdueNotification>
{
    private readonly INotifier _notifier;
    public BhxhTaxOverdueBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(BhxhTaxOverdueNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

/// <summary>v3.1 — line BHXH/TNCN của 1 NV được ghi nhận Paid → báo NV đó.</summary>
public sealed class BhxhTaxLinePaidBridge : INotificationHandler<BhxhTaxLinePaidNotification>
{
    private readonly INotifier _notifier;
    public BhxhTaxLinePaidBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(BhxhTaxLinePaidNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

/// <summary>v3.1 — line BHXH/TNCN của 1 NV bị đánh dấu Excluded → báo NV đó.</summary>
public sealed class BhxhTaxLineExcludedBridge : INotificationHandler<BhxhTaxLineExcludedNotification>
{
    private readonly INotifier _notifier;
    public BhxhTaxLineExcludedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(BhxhTaxLineExcludedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

// ===== Phase 5 — Compliance =====

public sealed class CompliancePendingSoonBridge : INotificationHandler<CompliancePendingSoonNotification>
{
    private readonly INotifier _notifier;
    public CompliancePendingSoonBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(CompliancePendingSoonNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class LegalDocumentDetectedBridge : INotificationHandler<LegalDocumentDetectedNotification>
{
    private readonly INotifier _notifier;
    public LegalDocumentDetectedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(LegalDocumentDetectedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class ProposalPendingReviewBridge : INotificationHandler<ProposalPendingReviewNotification>
{
    private readonly INotifier _notifier;
    public ProposalPendingReviewBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(ProposalPendingReviewNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

// ===== Phase 6 — Evaluation / Employee =====

public sealed class OrderEvaluationCreatedBridge : INotificationHandler<ProjectCompletedNotification>
{
    private readonly INotifier _notifier;
    public OrderEvaluationCreatedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(ProjectCompletedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class MonthlyEvaluationPublishedBridge : INotificationHandler<MonthlyEvaluationPublishedNotification>
{
    private readonly INotifier _notifier;
    public MonthlyEvaluationPublishedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(MonthlyEvaluationPublishedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class EmployeeDepartmentChangedBridge : INotificationHandler<EmployeeDepartmentChangedNotification>
{
    private readonly INotifier _notifier;
    public EmployeeDepartmentChangedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(EmployeeDepartmentChangedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class EmployeeSensitiveEditedBridge : INotificationHandler<EmployeeSensitiveEditedNotification>
{
    private readonly INotifier _notifier;
    public EmployeeSensitiveEditedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(EmployeeSensitiveEditedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class SalaryGradeChangedBridge : INotificationHandler<SalaryGradeChangedNotification>
{
    private readonly INotifier _notifier;
    public SalaryGradeChangedBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(SalaryGradeChangedNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}

public sealed class CommentMentionBridge : INotificationHandler<CommentMentionNotification>
{
    private readonly INotifier _notifier;
    public CommentMentionBridge(INotifier notifier) => _notifier = notifier;
    public Task Handle(CommentMentionNotification n, CancellationToken ct) => _notifier.RaiseAsync(n, ct);
}
