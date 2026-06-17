using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.LegalDocument.Commands;

public sealed class CreateLegalDocumentCommandHandler : IRequestHandler<CreateLegalDocumentCommand, int>
{
    private readonly ILegalDocumentRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateLegalDocumentCommandHandler(ILegalDocumentRepository repo, ICurrentUserService currentUser, IClock clock)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateLegalDocumentCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var entity = new Domain.Entities.Compliance.LegalDocument
        {
            Code = request.Code,
            DocumentType = request.DocumentType,
            DocumentNumber = request.DocumentNumber,
            Title = request.Title,
            IssuedBy = request.IssuedBy,
            IssuedDate = request.IssuedDate,
            EffectiveDate = request.EffectiveDate,
            ExpiryDate = request.ExpiryDate,
            Url = request.Url,
            Note = request.Note,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
        };

        return await _repo.AddAsync(entity, ct);
    }
}
