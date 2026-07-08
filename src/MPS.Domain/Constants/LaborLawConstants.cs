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
}
