using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Compliance;

namespace MPS.Application.Features.Compliance.LegalDocument.Queries;

public sealed record GetAllLegalDocumentsQuery(
    int PageNumber,
    int PageSize,
    string? DocumentType,
    bool? IsActive,
    string? Keyword) : IRequest<PagedResult<LegalDocumentDto>>;
