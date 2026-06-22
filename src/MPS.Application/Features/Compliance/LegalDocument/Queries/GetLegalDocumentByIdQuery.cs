using MediatR;
using MPS.Application.DTOs.Compliance;

namespace MPS.Application.Features.Compliance.LegalDocument.Queries;

public sealed record GetLegalDocumentByIdQuery(int Id) : IRequest<LegalDocumentDto?>;
