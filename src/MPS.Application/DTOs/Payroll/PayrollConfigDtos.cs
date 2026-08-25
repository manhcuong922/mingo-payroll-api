namespace MPS.Application.DTOs.Payroll;

/// <summary>Tóm tắt 1 bảng công thức (danh sách).</summary>
public sealed record FormulaSetDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    bool IsDefault,
    int ColumnCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>1 cột trong bảng công thức (đủ để trình dựng đọc/sửa).</summary>
public sealed record SalaryColumnDto(
    int Id,
    string ColumnCode,
    string Label,
    string? GroupName,
    int DisplayOrder,
    string DataType,
    string ValueSource,
    string? LookupKey,
    string? FormulaJson,
    string Rounding,
    bool IsSystem,
    bool IsMoney,
    bool IsEditable);

/// <summary>Bảng công thức + toàn bộ cột (chi tiết).</summary>
public sealed record FormulaSetDetailDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<SalaryColumnDto> Columns);

/// <summary>
/// Bậc hỗ trợ theo tiến độ của HĐ khoán việc: tổng giờ làm trong tháng vào khoảng
/// [MinHours, MaxHours] → trả <c>Amount</c>. <c>MaxHours</c> null = bậc trên cùng, không trần.
/// </summary>
public sealed record PieceWorkSupportBandDto(
    int Id,
    decimal MinHours,
    decimal? MaxHours,
    decimal Amount,
    string? Label,
    bool IsActive);

public sealed record ProductivityCoefficientDto(
    int Id,
    decimal MinScore,
    decimal MaxScore,
    decimal Coefficient,
    string? Label,
    bool IsActive);
