using MediatR;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractTemplate.Commands;

public sealed record ContractTemplateCreateCommand(
    string Name,
    ContractKind Kind,
    ContractCategory? ContractCategory,
    ContractSubtype? ContractSubtype,
    bool IsActive) : IRequest<int>;
