using MediatR;

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
