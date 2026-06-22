using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Phân loại severity theo 2 tầng (rẻ-trước → đắt-sau): Module → Field/Delete.
/// Tầng nào match trước thì dừng. Default = Low.
///
/// Triết lý: High = thay đổi dữ liệu có ý nghĩa pháp lý / không thể đảo ngược / ảnh
/// hưởng tính lương toàn hệ thống. AccessDenied, User CRUD nhẹ (LastLoginAt, …),
/// và Delete trên module không đăng ký (vd notification user tự xóa thông báo)
/// đều là Low — vẫn được audit nhưng không giữ vĩnh viễn để tránh phình archive.
/// </summary>
public sealed class DefaultAuditSeverityClassifier : IAuditSeverityClassifier
{
    private static readonly HashSet<string> AlwaysHighModules = new(StringComparer.Ordinal)
    {
        "Role",
        "Permission",
        "RolePermission",
        "ContractTermination",
        "SocialInsuranceRateVersion",
        "PitRegulationVersion",
        "OtRateVersion",
        "MinimumWageVersion",
        "AllowanceExemptThresholdVersion",
        "LaborLawConstantsVersion",
    };

    private static readonly Dictionary<string, HashSet<string>> SensitiveFieldsByModule = new(StringComparer.Ordinal)
    {
        ["Contract"] = new(StringComparer.Ordinal)
        {
            "Status", "EndDate", "BaseSalary", "ContractType",
        },
        ["ContractAllowance"] = new(StringComparer.Ordinal)
        {
            "Amount", "Status",
        },
        ["EmployeeInformation"] = new(StringComparer.Ordinal)
        {
            "BankNumber", "CitizenId", "TaxNo", "SocialNo", "UserId",
        },
        ["Position"] = new(StringComparer.Ordinal)
        {
            "BaseSalary", "SalaryGradeId",
        },
        // User chỉ High khi đổi field bảo mật. LastLoginAt/PhoneNo/FullName… → Low.
        ["User"] = new(StringComparer.Ordinal)
        {
            "PasswordHash", "RoleId", "Status",
        },
    };

    public AuditSeverity Classify(string module, AuditAction action, IReadOnlyCollection<string> changedFields)
    {
        // Tầng 1 (Action) đã bỏ — không còn shortcut "mọi Delete = High".
        // Delete giờ phụ thuộc module có nhạy cảm hay không.

        // Tầng 2 — Module (10 modules pháp lý / quyền hạn) → bất kỳ action nào cũng High
        if (AlwaysHighModules.Contains(module))
            return AuditSeverity.High;

        // Tầng 3 — Module có sensitive fields
        if (SensitiveFieldsByModule.TryGetValue(module, out var sensitive))
        {
            // Delete entity nhạy cảm = mất evidence → High
            if (action == AuditAction.Delete)
                return AuditSeverity.High;

            // Update field nhạy cảm → High
            if (action == AuditAction.Update && changedFields.Any(sensitive.Contains))
                return AuditSeverity.High;
        }

        return AuditSeverity.Low;
    }
}
