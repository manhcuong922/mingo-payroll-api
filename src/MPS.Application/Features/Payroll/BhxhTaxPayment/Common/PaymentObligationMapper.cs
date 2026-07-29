using MPS.Application.DTOs.Payroll;
using MPS.Domain.Entities.Payroll.BhxhTax;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Common;

/// <summary>
/// Map entity → DTO (v3). Cần lấy Year/Month từ nav PayrollPeriod — caller phải Include khi load.
/// Các field aggregate (counts/amounts) truyền vào từ query riêng để không bắt caller load toàn bộ line.
/// </summary>
internal static class PaymentObligationMapper
{
    public static PaymentObligationDto Map(
        PaymentObligation o,
        DateTime today,
        PaymentObligationLineCounts counts,
        PaymentObligationLineAggregates aggregates,
        int evidenceCount)
    {
        var days = (int)(o.Deadline.Date - today.Date).TotalDays;
        var remaining = o.AmountDue - aggregates.PaidAmount - aggregates.ExcludedAmount;
        var canBeCompleted = o.Status != PaymentObligationStatus.Completed
                             && counts.AllHandled
                             && evidenceCount >= 1;

        return new PaymentObligationDto(
            Id: o.Id,
            PayrollPeriodId: o.PayrollPeriodId,
            PeriodYear: o.PayrollPeriod?.PeriodYear ?? 0,
            PeriodMonth: o.PayrollPeriod?.PeriodMonth ?? 0,
            ObligationType: o.ObligationType,
            AmountDue: o.AmountDue,
            Deadline: o.Deadline,
            DaysUntilDeadline: days,
            Status: o.Status,
            Notes: o.Notes,
            CompletedAt: o.CompletedAt,
            CompletedByUserId: o.CompletedByUserId,
            CreatedAt: o.CreatedAt,
            UpdatedAt: o.UpdatedAt,
            PaidAmountAggregate: aggregates.PaidAmount,
            ExcludedAmountAggregate: aggregates.ExcludedAmount,
            RemainingAmount: remaining,
            TotalLinesCount: counts.Total,
            PaidLinesCount: counts.Paid,
            ExcludedLinesCount: counts.Excluded,
            PendingLinesCount: counts.Pending,
            EvidenceCount: evidenceCount,
            CanBeCompleted: canBeCompleted);
    }

    public static PaymentObligationLineDto MapLine(PaymentObligationLine line) =>
        new(
            Id: line.Id,
            PaymentObligationId: line.PaymentObligationId,
            EmployeeInformationId: line.EmployeeInformationId,
            EmployeeCode: line.EmployeeCodeSnapshot,
            EmployeeFullName: line.EmployeeFullNameSnapshot,
            EmployeeContribution: line.EmployeeContribution,
            EmployerContribution: line.EmployerContribution,
            TotalAmount: line.TotalAmount,
            LineStatus: line.LineStatus,
            PaidDate: line.PaidDate,
            PaidAmount: line.PaidAmount,
            PaymentMethod: line.PaymentMethod,
            TransactionRef: line.TransactionRef,
            PaymentNotes: line.PaymentNotes,
            RecordedByUserId: line.RecordedByUserId,
            RecordedByUserName: line.RecordedByUser?.Username,
            RecordedAt: line.RecordedAt,
            ExcludedReason: line.ExcludedReason,
            ExcludedByUserId: line.ExcludedByUserId,
            ExcludedByUserName: line.ExcludedByUser?.Username,
            ExcludedAt: line.ExcludedAt,
            RowVersion: Convert.ToBase64String(line.RowVersion ?? Array.Empty<byte>()));

    public static PaymentObligationEvidenceDto MapEvidence(
        PaymentObligationEvidence evidence, string? presignedUrl) =>
        new(
            Id: evidence.Id,
            PaymentObligationId: evidence.PaymentObligationId,
            OriginalFileName: evidence.OriginalFileName,
            ContentType: evidence.ContentType,
            SizeBytes: evidence.SizeBytes,
            Description: evidence.Description,
            UploadedByUserId: evidence.UploadedByUserId,
            UploadedByUserName: evidence.UploadedByUser?.Username,
            UploadedAt: evidence.UploadedAt,
            PresignedDownloadUrl: presignedUrl);
}
