using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Compliance.AutoUpdate.Approval;

/// <summary>Kết quả duyệt/từ chối proposal.</summary>
public sealed record ComplianceProposalActionResult(int ProposalId, string Status, int? NewVersionId);

public sealed record ApproveComplianceProposalCommand(int Id, string? Note) : IRequest<ComplianceProposalActionResult>;
public sealed record RejectComplianceProposalCommand(int Id, string? Note) : IRequest<ComplianceProposalActionResult>;

public sealed class ApproveComplianceProposalCommandHandler
    : IRequestHandler<ApproveComplianceProposalCommand, ComplianceProposalActionResult>
{
    private readonly IComplianceUpdateProposalRepository _repo;
    private readonly ComplianceProposalApplier _applier;
    private readonly IClock _clock;

    public ApproveComplianceProposalCommandHandler(
        IComplianceUpdateProposalRepository repo, ComplianceProposalApplier applier, IClock clock)
    {
        _repo = repo;
        _applier = applier;
        _clock = clock;
    }

    public async Task<ComplianceProposalActionResult> Handle(ApproveComplianceProposalCommand request, CancellationToken ct)
    {
        var proposal = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Proposal #{request.Id} không tồn tại.");

        if (proposal.Status != ProposalStatuses.PendingReview)
            throw new ConflictException($"Proposal #{request.Id} đã ở trạng thái '{proposal.Status}', không thể duyệt.");

        // Tạo version mới (chờ hiệu lực) trước; nếu lỗi, proposal vẫn giữ PendingReview.
        var newVersionId = await _applier.ApplyAsync(proposal, ct);

        proposal.Status = ProposalStatuses.Approved;
        proposal.ReviewNote = request.Note;
        proposal.ReviewedAt = _clock.UtcNow;
        await _repo.SaveChangesAsync(ct);

        return new ComplianceProposalActionResult(proposal.Id, proposal.Status, newVersionId);
    }
}

public sealed class RejectComplianceProposalCommandHandler
    : IRequestHandler<RejectComplianceProposalCommand, ComplianceProposalActionResult>
{
    private readonly IComplianceUpdateProposalRepository _repo;
    private readonly IClock _clock;

    public RejectComplianceProposalCommandHandler(IComplianceUpdateProposalRepository repo, IClock clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public async Task<ComplianceProposalActionResult> Handle(RejectComplianceProposalCommand request, CancellationToken ct)
    {
        var proposal = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Proposal #{request.Id} không tồn tại.");

        if (proposal.Status != ProposalStatuses.PendingReview)
            throw new ConflictException($"Proposal #{request.Id} đã ở trạng thái '{proposal.Status}', không thể từ chối.");

        proposal.Status = ProposalStatuses.Rejected;
        proposal.ReviewNote = request.Note;
        proposal.ReviewedAt = _clock.UtcNow;
        await _repo.SaveChangesAsync(ct);

        return new ComplianceProposalActionResult(proposal.Id, proposal.Status, null);
    }
}
