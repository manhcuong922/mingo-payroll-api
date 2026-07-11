using System.Globalization;
using MPS.Application.Common.Models.Compliance;
using MPS.Domain.Entities.Compliance;

namespace MPS.Application.Features.Compliance.LaborLawConstants;

/// <summary>
/// So sánh 2 phiên bản Hằng số luật lao động → danh sách thay đổi số "từ A thành B"
/// (chỉ gồm field thực sự đổi). Dùng để thông báo cho toàn bộ nhân sự khi luật đổi con số.
/// </summary>
public static class LaborLawConstantsDiff
{
    public const string DisplayName = "Hằng số luật lao động";

    public static IReadOnlyList<ComplianceFieldChange> Build(
        LaborLawConstantsVersion? oldVersion,
        LaborLawConstantsVersion newVersion)
    {
        if (oldVersion is null) return Array.Empty<ComplianceFieldChange>();

        var changes = new List<ComplianceFieldChange>();

        AddPercent(changes, "Lương thử việc tối thiểu", oldVersion.ProbationSalaryMinPercent, newVersion.ProbationSalaryMinPercent);
        AddPercent(changes, "Phụ trội làm thêm ban đêm", oldVersion.OvertimeNightExtraPercent, newVersion.OvertimeNightExtraPercent);
        AddPercent(changes, "Phụ cấp làm đêm", oldVersion.NightWorkSurchargePercent, newVersion.NightWorkSurchargePercent);
        AddPercent(changes, "Phụ trội tăng ca ngày thường", oldVersion.OvertimeNormalUpliftPercent, newVersion.OvertimeNormalUpliftPercent);
        AddTime(changes, "Giờ bắt đầu ca đêm", oldVersion.NightStartTime, newVersion.NightStartTime);
        AddTime(changes, "Giờ kết thúc ca đêm", oldVersion.NightEndTime, newVersion.NightEndTime);
        AddNumber(changes, "Giờ làm chuẩn/ngày", oldVersion.StandardWorkingHoursPerDay, newVersion.StandardWorkingHoursPerDay);
        AddNumber(changes, "Giờ thường tối đa/tuần", oldVersion.MaxRegularHoursPerWeek, newVersion.MaxRegularHoursPerWeek);
        AddNumber(changes, "Ngày công chuẩn/tháng", oldVersion.StandardWorkingDaysPerMonth, newVersion.StandardWorkingDaysPerMonth);
        AddInt(changes, "Ngày phép năm tối thiểu", oldVersion.AnnualLeaveMinDays, newVersion.AnnualLeaveMinDays);
        AddPercent(changes, "Đơn giá ngày thường (sàn)", oldVersion.WorkRateNormalFloor, newVersion.WorkRateNormalFloor);
        AddPercent(changes, "Đơn giá ngày nghỉ tuần (sàn)", oldVersion.WorkRateWeeklyRestFloor, newVersion.WorkRateWeeklyRestFloor);
        AddPercent(changes, "Đơn giá ngày lễ (sàn)", oldVersion.WorkRateHolidayFloor, newVersion.WorkRateHolidayFloor);
        AddNumber(changes, "Tăng ca tối đa/ngày", oldVersion.OvertimeMaxHoursPerDay, newVersion.OvertimeMaxHoursPerDay);
        AddNumber(changes, "Tăng ca tối đa/tháng", oldVersion.OvertimeMaxHoursPerMonth, newVersion.OvertimeMaxHoursPerMonth);
        AddInt(changes, "Tăng ca tối đa/năm", oldVersion.OvertimeMaxHoursPerYear, newVersion.OvertimeMaxHoursPerYear);

        return changes;
    }

    private static void AddPercent(List<ComplianceFieldChange> changes, string label, decimal oldVal, decimal newVal)
    {
        if (oldVal == newVal) return;
        changes.Add(new ComplianceFieldChange(label, $"{Fmt(oldVal)}%", $"{Fmt(newVal)}%"));
    }

    private static void AddNumber(List<ComplianceFieldChange> changes, string label, decimal oldVal, decimal newVal)
    {
        if (oldVal == newVal) return;
        changes.Add(new ComplianceFieldChange(label, Fmt(oldVal), Fmt(newVal)));
    }

    private static void AddInt(List<ComplianceFieldChange> changes, string label, int oldVal, int newVal)
    {
        if (oldVal == newVal) return;
        changes.Add(new ComplianceFieldChange(label, oldVal.ToString(CultureInfo.InvariantCulture), newVal.ToString(CultureInfo.InvariantCulture)));
    }

    private static void AddTime(List<ComplianceFieldChange> changes, string label, TimeOnly oldVal, TimeOnly newVal)
    {
        if (oldVal == newVal) return;
        changes.Add(new ComplianceFieldChange(label, oldVal.ToString("HH:mm"), newVal.ToString("HH:mm")));
    }

    private static string Fmt(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
