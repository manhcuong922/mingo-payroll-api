namespace MPS.API.Requests.SalaryGrade;

public sealed record CreateSalaryGradeRequest(
    string Code,
    string Name,
    int? PositionId,
    int? Level,
    decimal BaseSalary,
    string Currency,
    string? Description,
    bool IsActive = true);
