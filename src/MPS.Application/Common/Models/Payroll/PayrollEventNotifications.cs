using MediatR;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Common.Models.Payroll;

/// <summary>
/// Phase 4 §N11 — payslip cá nhân sẵn sàng xem, gửi ngay sau khi khoá kỳ. 1 event / 1 NV
/// (fan-out từ <see cref="PayrollPeriodLockedNotification"/>).
/// </summary>
public sealed record PayslipReadyNotification(
    int PeriodId,
    int PeriodYear,
    int PeriodMonth,
    int EmployeeUserId,
    decimal NetPay,
    string Currency) : INotification;

/// <summary>
/// Phase 4 §N9 — sắp đến hạn nộp BHXH / thuế: T-7, T-3, T-1 trước ngày due.
/// Job <c>BhxhTaxDeadlineJob</c> publish 3 event tương ứng khi phát hiện obligation trong khung ngày đó.
/// </summary>
public sealed record BhxhTaxDeadlineSoonNotification(
    int ObligationId,
    string ObligationLabel,
    DateTime DueDate,
    int DaysRemaining,   // 7 / 3 / 1
    decimal Amount,
    string Currency) : INotification;

/// <summary>
/// Phase 4 §N9 — đã quá hạn nộp BHXH / thuế: gửi HẰNG NGÀY cho tới khi obligation chuyển sang Paid.
/// Job idempotent theo (ObligationId, Date) — không gửi 2 lần cùng ngày.
/// </summary>
public sealed record BhxhTaxOverdueNotification(
    int ObligationId,
    string ObligationLabel,
    DateTime DueDate,
    int DaysOverdue,     // 1, 2, 3, …
    decimal Amount,
    string Currency) : INotification;

/// <summary>
/// v3.1 — HR/Accountant vừa ghi nhận đã nộp BHXH/TNCN của 1 NV (line-level). Báo cho chính NV đó.
/// <see cref="EmployeeUserId"/> có thể null nếu NV chưa gắn tài khoản đăng nhập —
/// template sẽ skip (return Nothing) trong trường hợp này.
/// </summary>
public sealed record BhxhTaxLinePaidNotification(
    int LineId,
    int ObligationId,
    PaymentObligationType ObligationType,
    int PeriodYear,
    int PeriodMonth,
    int EmployeeInformationId,
    int? EmployeeUserId,
    string EmployeeCodeSnapshot,
    string EmployeeFullNameSnapshot,
    decimal PaidAmount,
    DateTime PaidDate) : INotification;

/// <summary>
/// v3.1 — HR/Accountant đánh dấu bỏ qua line BHXH/TNCN của 1 NV (VD nghỉ việc trước hạn, đóng nơi khác).
/// Báo cho NV đó biết vì sao khoản của mình không được ghi nhận nộp.
/// </summary>
public sealed record BhxhTaxLineExcludedNotification(
    int LineId,
    int ObligationId,
    PaymentObligationType ObligationType,
    int PeriodYear,
    int PeriodMonth,
    int EmployeeInformationId,
    int? EmployeeUserId,
    string EmployeeCodeSnapshot,
    string EmployeeFullNameSnapshot,
    string ExcludedReason,
    DateTime ExcludedAt) : INotification;
