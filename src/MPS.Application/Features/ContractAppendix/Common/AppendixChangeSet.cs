namespace MPS.Application.Features.ContractAppendix.Common;

/// <summary>
/// Typed wrapper cho <c>ContractAppendix.ChangedClausesJson</c>. Bao gồm các điều khoản
/// HR có thể thay đổi qua phụ lục theo Điều 22 BLLĐ 2019:
/// — Mô tả công việc / địa điểm làm việc
/// — Mức lương: chuyển sang <see cref="SalaryGradeId"/> mới (snapshot không bị tự đóng băng
///   lại — phụ lục tự giữ giá trị trong PDF, ContractSalaryAmountSnapshot vẫn là của lần ký gốc;
///   tham chiếu thang lương cập nhật để báo cáo lương sau này).
/// — Phụ cấp: <see cref="AddAllowances"/> / <see cref="UpdateAllowances"/> /
///   <see cref="RemoveAllowanceTypeIds"/>
/// — Các trường free-text khác: <see cref="PromotionPolicy"/>, <see cref="PpePolicy"/>,
///   <see cref="TrainingPolicy"/>, <see cref="ConfidentialityClause"/>, <see cref="WorkingTimeJson"/>
///
/// <para>
/// <b>CẤM</b> sửa thời hạn (<c>EffectiveFrom</c>, <c>EffectiveTo</c>) và loại HĐ
/// (<c>ContractCategory</c>, <c>ContractSubtype</c>) — Điều 22 k.2. Validation chặn ở
/// <see cref="AppendixSchemaValidator"/>, không phải ở đây.
/// </para>
/// </summary>
public sealed record AppendixChangeSet
{
    public string? JobDescription { get; init; }
    public string? WorkLocation { get; init; }

    /// <summary>
    /// Đổi chức vụ qua phụ lục. Chỉ ảnh hưởng <c>Contract.PositionId</c>, KHÔNG đồng bộ ngược lên
    /// <c>EmployeeInformation.PositionId</c> — HR có thể để hai giá trị lệch nhau (hồ sơ giữ chức
    /// danh tổ chức, HĐ giữ chức danh pháp lý theo phụ lục).
    ///
    /// Nếu cùng phụ lục có thêm <see cref="SalaryGradeId"/>, bậc lương mới phải thuộc chức vụ mới
    /// (grade.PositionId == PositionId). Xem <see cref="AppendixApplier"/>.
    /// </summary>
    public int? PositionId { get; init; }
    public int? SalaryGradeId { get; init; }
    public string? PromotionPolicy { get; init; }
    public string? PpePolicy { get; init; }
    public string? TrainingPolicy { get; init; }
    public string? ConfidentialityClause { get; init; }
    public string? WorkingTimeJson { get; init; }

    public IReadOnlyList<AppendixAllowanceChange> AddAllowances { get; init; } = Array.Empty<AppendixAllowanceChange>();
    public IReadOnlyList<AppendixAllowanceChange> UpdateAllowances { get; init; } = Array.Empty<AppendixAllowanceChange>();
    public IReadOnlyList<int> RemoveAllowanceTypeIds { get; init; } = Array.Empty<int>();
}

/// <summary>
/// Một mục phụ cấp trong phụ lục. <see cref="Currency"/> null → kế thừa
/// <c>SalaryCurrencySnapshot</c> của HĐ gốc.
/// </summary>
public sealed record AppendixAllowanceChange(
    int AllowanceTypeId,
    decimal Amount,
    string? Currency = null,
    string? Note = null);
