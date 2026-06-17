using MediatR;

namespace MPS.Application.Features.Compliance.AllowanceExemptThreshold.Commands;

public sealed record CreateAllowanceExemptThresholdCommand(
    int AllowanceTypeId,
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds) : IRequest<int>;

public sealed record UpdateAllowanceExemptThresholdCommand(
    int Id,
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds) : IRequest<bool>;

public sealed record DeleteAllowanceExemptThresholdPendingCommand(int Id) : IRequest<bool>;
