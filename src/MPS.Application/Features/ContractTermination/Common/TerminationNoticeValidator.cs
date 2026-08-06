using MPS.Application.Features.Contract.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractTermination.Common;

/// <summary>
/// Pure validator cho thời gian báo trước khi chấm dứt HĐLĐ (Điều 35 + 36 BLLĐ 2019).
/// Không I/O — testable độc lập, không cần DB.
///
/// <para>
/// <b>Phạm vi:</b> Chỉ kiểm tra rule "thời gian báo trước" cho 2 nhóm party đơn phương
/// (<see cref="TerminationParty.Employee"/>, <see cref="TerminationParty.Employer"/>).
/// Các party còn lại (<see cref="TerminationParty.Mutual"/>, <see cref="TerminationParty.AutoExpiry"/>,
/// <see cref="TerminationParty.ByLaw"/>) không có ràng buộc notice — luôn pass.
/// </para>
///
/// <para>
/// <b>Văn bản thông báo</b> (Điều 45) — check riêng dựa trên
/// <see cref="ContractTerminationReasonRef.RequiresWrittenNotice"/>, không thuộc validator này.
/// </para>
/// </summary>
public static class TerminationNoticeValidator
{
    public static TerminationNoticeCheckResult Validate(TerminationNoticeCheckInput input)
    {
        // Các party này không cần báo trước — Điều 34 các khoản tự nhiên kết thúc.
        if (input.Party is TerminationParty.Mutual or TerminationParty.AutoExpiry or TerminationParty.ByLaw)
            return TerminationNoticeCheckResult.Ok();

        // Notice-period rules Điều 35/36 chỉ áp cho HĐ thuộc BLLĐ. Các HĐ dân sự (Service,
        // PieceWork, Collaborator) và HĐ đào tạo dùng notice-period thoả thuận trong HĐ,
        // không áp rule 45/30/3 ngày.
        if (ContractKindMetadata.Get(input.Kind).LegalFramework != ContractLegalFramework.LaborCode)
            return TerminationNoticeCheckResult.Ok();

        // Đối với đơn phương: nếu NLĐ/NSDLĐ rơi vào trường hợp ngoại lệ thì có thể bỏ báo trước
        // (Điều 35 k.2 cho NLĐ, Điều 36 k.3 cho NSDLĐ). Trust flag từ caller — handler set theo
        // ReasonRef hoặc HR khai báo cụ thể; ở đây chỉ verify "khi cờ bật thì pass".
        if (input.NoNoticeRequired)
            return TerminationNoticeCheckResult.Ok();

        // Số ngày báo trước = khoảng cách LỊCH thực tế NoticeSentAt → TerminationDate.
        // KHÔNG tin giá trị NoticeDaysAhead do client gửi (có thể bị khai khống 45 dù thực tế 20).
        var actualCalendarNoticeDays = CalendarDaysBetween(input.NoticeSentAt, input.TerminationDate);

        // HĐ vô thời hạn → cả 2 bên đều phải báo ≥ 45 ngày lịch.
        if (input.Category == ContractCategory.Indefinite)
        {
            return RequireMinDays(
                actualCalendarNoticeDays,
                LaborLawConstants.TerminationNotice.EmployeeNoticeDaysIndefinite,
                "TERMINATION_NOTICE_INDEFINITE_TOO_SHORT");
        }

        // HĐ XĐ thời hạn → phân theo độ dài.
        var durationDays = ContractDurationDays(input.EffectiveFrom, input.EffectiveTo);
        if (durationDays is null)
        {
            // Definite mà thiếu EffectiveTo → input không nhất quán (lẽ ra validator HĐ chặn rồi).
            return TerminationNoticeCheckResult.Fail(
                "TERMINATION_NOTICE_DEFINITE_MISSING_END",
                "Definite contract requires EffectiveTo to compute notice rule.");
        }

        // Ranh giới 12 tháng tính theo 365 ngày — đủ chính xác cho rule lao động.
        const int twelveMonthsInDays = 365;

        if (durationDays >= twelveMonthsInDays)
        {
            // 12–36 tháng → ≥ 30 ngày lịch.
            return RequireMinDays(
                actualCalendarNoticeDays,
                LaborLawConstants.TerminationNotice.EmployeeNoticeDaysDefinite12To36M,
                "TERMINATION_NOTICE_DEFINITE_12_36M_TOO_SHORT");
        }

        // < 12 tháng → ≥ 3 ngày LÀM VIỆC (không phải ngày lịch).
        // Cần đối chiếu khoảng NoticeSentAt → TerminationDate theo working day.
        var workingDayGap = WorkingDayCalculator.CountWorkingDaysBetween(
            input.NoticeSentAt, input.TerminationDate);

        if (workingDayGap < LaborLawConstants.TerminationNotice.EmployeeNoticeWorkingDaysDefiniteUnder12M)
        {
            return TerminationNoticeCheckResult.Fail(
                "TERMINATION_NOTICE_DEFINITE_UNDER_12M_TOO_SHORT",
                $"NoticeSentAt → TerminationDate must span at least " +
                $"{LaborLawConstants.TerminationNotice.EmployeeNoticeWorkingDaysDefiniteUnder12M} working days " +
                $"for definite contracts under 12 months (currently {workingDayGap}).");
        }

        return TerminationNoticeCheckResult.Ok();
    }

    private static TerminationNoticeCheckResult RequireMinDays(int actualNoticeDays, int minDays, string errorCode)
    {
        if (actualNoticeDays < minDays)
        {
            return TerminationNoticeCheckResult.Fail(
                errorCode,
                $"Notice period (NoticeSentAt → TerminationDate) must be at least {minDays} calendar days " +
                $"(actual: {actualNoticeDays}).");
        }
        return TerminationNoticeCheckResult.Ok();
    }

    private static int CalendarDaysBetween(DateTime noticeSentAt, DateTime terminationDate)
    {
        var gap = (terminationDate.Date - noticeSentAt.Date).TotalDays;
        return (int)Math.Max(0, gap);
    }

    private static int? ContractDurationDays(DateTime effectiveFrom, DateTime? effectiveTo)
    {
        if (effectiveTo is null) return null;
        return (int)Math.Max(0, (effectiveTo.Value.Date - effectiveFrom.Date).TotalDays);
    }
}

public sealed record TerminationNoticeCheckInput(
    TerminationParty Party,
    ContractKind Kind,
    ContractCategory? Category,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    DateTime NoticeSentAt,
    DateTime TerminationDate,
    int? NoticeDaysAhead,
    bool NoNoticeRequired);

public sealed record TerminationNoticeCheckResult(bool IsValid, string? ErrorCode, string? ErrorMessage)
{
    public static TerminationNoticeCheckResult Ok() => new(true, null, null);
    public static TerminationNoticeCheckResult Fail(string code, string message) => new(false, code, message);
}
