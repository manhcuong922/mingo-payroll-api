namespace MPS.Application.DTOs;

public sealed record SalaryGradeDto(
    int Id,
    string Code,
    string Name,
    int? PositionId,
    int? Level,
    decimal BaseSalary,
    string Currency,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
