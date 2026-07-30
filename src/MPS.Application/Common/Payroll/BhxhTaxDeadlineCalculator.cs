using MPS.Domain.Constants;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Common.Payroll;

/// <summary>
/// Tính deadline nộp BHXH/TNCN cho 1 kỳ lương (năm, tháng).
/// BHXH: ngày 15 của tháng liền sau. TNCN (chu kỳ tháng): ngày 20 của tháng liền sau.
/// </summary>
public static class BhxhTaxDeadlineCalculator
{
    public static DateTime For(PaymentObligationType type, int periodYear, int periodMonth)
    {
        if (periodMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(periodMonth), periodMonth, "Tháng phải trong 1–12.");
        if (periodYear is < 2000 or > 2100)
            throw new ArgumentOutOfRangeException(nameof(periodYear), periodYear, "Năm không hợp lệ.");

        var (nextYear, nextMonth) = NextMonth(periodYear, periodMonth);
        var day = type switch
        {
            PaymentObligationType.Bhxh => LaborLawConstants.PaymentObligationDeadline.BhxhDayOfNextMonth,
            PaymentObligationType.Tncn => LaborLawConstants.PaymentObligationDeadline.TncnMonthlyDayOfNextMonth,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
        return new DateTime(nextYear, nextMonth, day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static (int Year, int Month) NextMonth(int year, int month) =>
        month == 12 ? (year + 1, 1) : (year, month + 1);
}
