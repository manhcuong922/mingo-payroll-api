using MediatR;

namespace MPS.Application.Features.Compliance.AllowanceExemptThreshold.Commands;

public sealed record CreateAllowanceExemptThresholdCommand(
    int AllowanceTypeId,
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string LegalReference,
    string? Note) : IRequest<int>;

public sealed record UpdateAllowanceExemptThresholdCommand(
    int Id,
    string ThresholdMode,
    decimal? MonthlyExemptAmount,
    bool AppliesToTax,
    bool AppliesToSocial,
    string LegalReference,
    string? Note) : IRequest<bool>;

public sealed record DeleteAllowanceExemptThresholdPendingCommand(int Id) : IRequest<bool>;
