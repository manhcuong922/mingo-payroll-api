using MediatR;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractTemplate.Commands;

public sealed record ContractTemplateUpdateCommand(
    int Id,
    string Name,
    ContractCategory ContractCategory,
    ContractSubtype ContractSubtype,
    string TemplateBody,
    bool IsActive) : IRequest<bool>;
