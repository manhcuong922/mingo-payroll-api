using System.Globalization;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Common;

/// <summary>
/// Sinh tên bảng phân ca mặc định khi user không nhập.
/// Format: "{ca} - {đối tượng} - {phạm vi}" — mỗi phần bỏ qua nếu không có dữ liệu.
///
/// Ca: shift code đầu tiên; nhiều ca (2) → "CODE1+CODE2"; >2 → "{N} ca".
/// Đối tượng: mode Departments → "PB {tên}"; mode Employees 1-2 NV hiện tên, >2 → "{N} NV".
/// Phạm vi: cùng tháng → "MM/yyyy"; cùng năm → "MM-MM/yyyy"; khác năm → "MM/yyyy-MM/yyyy".
///
/// Duplicate handling: nếu tên đã tồn tại (active) → thêm suffix " (2)", " (3)"... cho unique.
/// </summary>
internal static class ScheduleTemplateNameGenerator
{
    private const string FallbackName = "Bảng phân ca mới";

    public static async Task<string> GenerateAsync(
        IReadOnlyCollection<int> shiftTemplateIds,
        ScheduleApplyTarget? applyTarget,
        IReadOnlyCollection<int>? employeeInformationIds,
        IReadOnlyCollection<int>? departmentIds,
        DateOnly effectiveFrom,
        DateOnly effectiveTo,
        int? excludeTemplateId,
        IShiftTemplateRepository shiftRepo,
        IDepartmentRepository deptRepo,
        IEmployeeInformationRepository empRepo,
        IScheduleTemplateRepository templateRepo,
        CancellationToken cancellationToken)
    {
        var parts = new List<string>();

        var shiftPart = await BuildShiftPartAsync(shiftTemplateIds, shiftRepo, cancellationToken);
        if (!string.IsNullOrEmpty(shiftPart)) parts.Add(shiftPart);

        var targetPart = await BuildTargetPartAsync(
            applyTarget, employeeInformationIds, departmentIds, deptRepo, empRepo, cancellationToken);
        if (!string.IsNullOrEmpty(targetPart)) parts.Add(targetPart);

        var rangePart = BuildRangePart(effectiveFrom, effectiveTo);
        if (!string.IsNullOrEmpty(rangePart)) parts.Add(rangePart);

        var baseName = parts.Count == 0 ? FallbackName : string.Join(" - ", parts);

        // Handle duplicate — chỉ enforce với template active (DB unique index đã filter IsActive=1).
        var candidate = baseName;
        var suffix = 2;
        while (await templateRepo.ExistsByNameAsync(candidate, excludeTemplateId, cancellationToken))
        {
            candidate = $"{baseName} ({suffix})";
            suffix++;
            if (suffix > 999) break; // safety
        }
        return candidate;
    }

    private static async Task<string> BuildShiftPartAsync(
        IReadOnlyCollection<int> shiftTemplateIds,
        IShiftTemplateRepository shiftRepo,
        CancellationToken cancellationToken)
    {
        var uniqueIds = shiftTemplateIds.Where(id => id > 0).Distinct().ToList();
        if (uniqueIds.Count == 0) return string.Empty;

        if (uniqueIds.Count > 2) return $"{uniqueIds.Count} ca";

        var codes = new List<string>(uniqueIds.Count);
        foreach (var id in uniqueIds)
        {
            var shift = await shiftRepo.GetByIdAsync(id, cancellationToken);
            if (shift is not null) codes.Add(shift.Code);
        }
        return string.Join("+", codes);
    }

    private static async Task<string> BuildTargetPartAsync(
        ScheduleApplyTarget? applyTarget,
        IReadOnlyCollection<int>? employeeInformationIds,
        IReadOnlyCollection<int>? departmentIds,
        IDepartmentRepository deptRepo,
        IEmployeeInformationRepository empRepo,
        CancellationToken cancellationToken)
    {
        if (applyTarget == ScheduleApplyTarget.Departments)
        {
            var ids = departmentIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return string.Empty;

            // Fetch dept names bulk — GetAllAsync trả active depts, dept đã ngừng sẽ không có tên → bỏ qua
            var allDepts = await deptRepo.GetAllAsync(includeInactive: true, keyword: null, ct: cancellationToken);
            var byId = allDepts.ToDictionary(d => d.Id, d => d.Name);
            var firstName = ids.Where(byId.ContainsKey).Select(id => byId[id]).FirstOrDefault();
            if (firstName is null) return $"{ids.Count} PB";
            return ids.Count == 1 ? $"PB {firstName}" : $"PB {firstName}+{ids.Count - 1}";
        }

        var empIds = employeeInformationIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
        if (empIds.Count == 0) return string.Empty;
        if (empIds.Count > 2) return $"{empIds.Count} NV";

        var firstEmp = await empRepo.GetByIdAsync(empIds[0], cancellationToken);
        if (firstEmp is null) return $"{empIds.Count} NV";
        return empIds.Count == 1 ? $"NV {firstEmp.FullName}" : $"NV {firstEmp.FullName}+1";
    }

    private static string BuildRangePart(DateOnly from, DateOnly to)
    {
        if (from == default || to == default) return string.Empty;
        var invariant = CultureInfo.InvariantCulture;
        if (from.Year == to.Year && from.Month == to.Month)
        {
            return from.ToString("MM/yyyy", invariant);
        }
        if (from.Year == to.Year)
        {
            return $"{from.ToString("MM", invariant)}-{to.ToString("MM/yyyy", invariant)}";
        }
        return $"{from.ToString("MM/yyyy", invariant)}-{to.ToString("MM/yyyy", invariant)}";
    }
}
