using MediatR;

namespace MPS.Application.Features.ContractTemplate.Commands;

public sealed record ContractTemplateDeleteCommand(int Id) : IRequest<bool>;
