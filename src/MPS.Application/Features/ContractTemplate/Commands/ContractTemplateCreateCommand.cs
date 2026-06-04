using MediatR;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractTemplate.Commands;

public sealed record ContractTemplateCreateCommand(
    string Name,
    ContractCategory ContractCategory,
    ContractSubtype ContractSubtype,
    string TemplateBody,
    bool IsActive) : IRequest<int>;
