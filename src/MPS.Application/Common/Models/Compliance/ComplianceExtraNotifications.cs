using MediatR;

namespace MPS.Application.Common.Models.Compliance;

/// <summary>
/// Phase 5 — bản pending sắp tự active. Job <c>ComplianceActivationJob</c> publish khi phát hiện
/// pending còn ≤ 7 / ≤ 1 ngày tới thời điểm active.
/// </summary>
public sealed record CompliancePendingSoonNotification(
    string ComplianceType,
    string DisplayName,
    DateTime EffectiveFrom,
    int DaysRemaining) : INotification;

/// <summary>
/// Phase 5 — Auto-Update watcher phát hiện văn bản pháp luật mới cần review.
/// </summary>
public sealed record LegalDocumentDetectedNotification(
    int LegalDocumentId,
    string DocumentTitle,
    string SourceUrl) : INotification;

/// <summary>Phase 5 — Auto-Update tạo đề xuất chờ compliance officer duyệt.</summary>
public sealed record ProposalPendingReviewNotification(
    int ProposalId,
    string ComplianceType,
    string Summary) : INotification;
