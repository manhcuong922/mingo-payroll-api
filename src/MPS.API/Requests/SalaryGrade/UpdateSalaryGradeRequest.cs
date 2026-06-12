namespace MPS.API.Requests.SalaryGrade;

public sealed record UpdateSalaryGradeRequest(
    string Name,
    decimal BaseSalary,
    string Currency,
    string? Description,
    bool IsActive);
