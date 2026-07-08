using MPS.Application.Services;

namespace MPS.API.Requests.Project;

public record CreateProjectRequest(
    string Title,
    string? Description,
    DateOnly? StartDate,
    DateTime? Deadline,
    int? DepartmentId);

public record UpdateProjectRequest(
    string Title,
    string? Description,
    DateTime? Deadline,
    string? Status);

public record GenerateTasksRequest(
    int ProcessTemplateId,
    IReadOnlyList<GenerateStepOverride>? Overrides);

public record CompleteProjectRequest(string? CompletionNote);
