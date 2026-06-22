using MediatR;

namespace MPS.Application.Features.Compliance.LegalDocument.Commands;

public sealed record DeactivateLegalDocumentCommand(int Id) : IRequest<bool>;
