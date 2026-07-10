namespace MPS.API.Requests.Project;

public record CreateProjectRequest(
    string Title,
    string? Description,
    DateOnly? StartDate,
    DateTime? Deadline,
    int? DepartmentId,
    int Quantity,               // >0 — hệ số scale sản lượng
    int ResponsibleUserId);     // tổ trưởng sản xuất (role 6, cùng xưởng)

public record UpdateProjectRequest(
    string Title,
    string? Description,
    DateOnly? StartDate,        // sửa StartDate → cascade co giãn deadline mọi task
    DateTime? Deadline,
    string? Status);

public record GenerateTasksRequest(int ProcessTemplateId);

public record SetResponsibleRequest(int ResponsibleUserId);

public record CompleteProjectRequest(string? CompletionNote, DateTime? CompletedAt = null);
