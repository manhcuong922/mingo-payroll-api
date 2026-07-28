namespace MPS.Domain.Constants;

public static class LaborLawConstants
{
    public static class ContractForm
    {
        public const int MinDurationForWrittenFormDays = 30;
        public const int RequiredPaperCopies = 2;
        public const bool ElectronicEqualsWritten = true;
    }

    public static class SigningAuthority
    {
        public const int EmployeeFullCapacityAge = 18;
    }

    public static class ContractDuration
    {
        public const int DefiniteTermMaxMonths = 36;
        public const int AutoConvertToIndefiniteAfterDays = 30;
        public const int DefiniteTermRenewalMaxTimes = 1;
    }

    public static class Probation
    {
        public const int NotAllowedBelowDays = 30;
        public const int MaxDaysEnterpriseManager = 180;
        public const int MaxDaysCollegeOrHigher = 60;
        public const int MaxDaysIntermediate = 30;
        public const int MaxDaysOther = 6;
        public const decimal MinSalaryPercent = 85m;
        public const bool CancelRequiresNotice = false;
    }

    public static class Appendix
    {
        public const bool CanChangeDuration = false;
        public const int AmendmentNoticeMinWorkingDays = 3;
    }

    public static class TerminationNotice
    {
        public const int EmployeeNoticeDaysIndefinite = 45;
        public const int EmployeeNoticeDaysDefinite12To36M = 30;
        public const int EmployeeNoticeWorkingDaysDefiniteUnder12M = 3;

        public const int EmployerNoticeDaysIndefinite = 45;
        public const int EmployerNoticeDaysDefinite12To36M = 30;
        public const int EmployerNoticeWorkingDaysDefiniteUnder12M = 3;
    }

    public static class PartTime
    {
        public const int MaxHoursPerWeek = 48;
    }

    public static class VocationalTraining
    {
        public const int MinCommitmentMonths = 1;
        public const int MaxCommitmentMonths = 60;
    }

    public static class Retention
    {
        public const int ContractRetentionYearsAfterTermination = 10;
        public const int LaborDisputeStatuteOfLimitationsYears = 1;
    }

    public static class Currency
    {
        public const string Default = "VND";
    }

    /// <summary>
    /// Deadline nộp nghĩa vụ BHXH & TNCN cho module BHXH & Tax Payment Tracking.
    /// Deadline = ngày thứ N của tháng liền sau kỳ lương (kỳ 2026-07 ⇒ BHXH hạn 2026-08-15,
    /// TNCN hạn 2026-08-20). Chu kỳ TNCN mặc định là hàng THÁNG.
    /// </summary>
    public static class PaymentObligationDeadline
    {
        public const int BhxhDayOfNextMonth = 15;
        public const int TncnMonthlyDayOfNextMonth = 20;
    }

    /// <summary>
    /// Mã 13 lý do chấm dứt HĐLĐ theo Điều 34 BLLĐ 2019 – dùng để seed ContractTerminationReasonRefs.
    /// </summary>
    public static class TerminationReasonCodes
    {
        public const string ExpiryC1 = "ART34_C1_EXPIRY";
        public const string WorkCompletedC2 = "ART34_C2_WORK_COMPLETED";
        public const string MutualAgreementC3 = "ART34_C3_MUTUAL_AGREEMENT";
        public const string CriminalSentenceC4 = "ART34_C4_CRIMINAL_SENTENCE";
        public const string ForeignerDeportedC5 = "ART34_C5_FOREIGNER_DEPORTED";
        public const string EmployeeDeathC6 = "ART34_C6_EMPLOYEE_DEATH_OR_INCAPACITY";
        public const string EmployerDissolvedC7 = "ART34_C7_EMPLOYER_DEATH_OR_DISSOLVED";
        public const string DisciplinaryDismissalC8 = "ART34_C8_DISCIPLINARY_DISMISSAL";
        public const string EmployeeUnilateralC9 = "ART34_C9_EMPLOYEE_UNILATERAL";
        public const string EmployerUnilateralC10 = "ART34_C10_EMPLOYER_UNILATERAL";
        public const string EmployerLayoffC11 = "ART34_C11_EMPLOYER_LAYOFF";
        public const string WorkPermitExpiredC12 = "ART34_C12_WORK_PERMIT_EXPIRED";
        public const string ProbationFailedC13 = "ART34_C13_PROBATION_FAILED";
    }

    /// <summary>
    /// Đ.112 BLLĐ 2019 — ngày nghỉ lễ hưởng nguyên lương. Chỉ liệt kê các mốc DƯƠNG LỊCH cố định;
    /// Tết Âm lịch (5 ngày) và Giỗ Tổ 10/3 âm lịch cần lịch âm nên phải nhập tay vào bảng
    /// <c>PublicHolidays</c> (xem cảnh báo readiness — HOL-WARN01/[D17]).
    /// </summary>
    public static class PublicHolidays
    {
        /// <summary>(Tháng, Ngày) của các mốc lễ dương lịch cố định.</summary>
        public static readonly IReadOnlyList<(int Month, int Day)> FixedSolarDates = new[]
        {
            (1, 1),   // Tết Dương lịch
            (4, 30),  // Ngày Giải phóng miền Nam
            (5, 1),   // Quốc tế Lao động
            (9, 2),   // Quốc khánh 02/9 (ngày liền kề do NSDLĐ chọn hằng năm ⇒ không cố định, không kể ở đây)
        };

        /// <summary>Các ngày lễ dương lịch cố định RƠI VÀO tháng đang xét.</summary>
        public static IEnumerable<DateOnly> FixedSolarInMonth(int year, int month) =>
            FixedSolarDates.Where(h => h.Month == month).Select(h => new DateOnly(year, h.Month, h.Day));
    }
}
