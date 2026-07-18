using MediatR;

namespace MPS.Application.Common.Models.Evaluation;

/// <summary>
/// Phát khi 1 đơn (<c>Project</c>) chuyển Completed — handler sinh bảng đánh giá theo đơn (D9).
/// Xem plan §4.1, §13.7.
/// </summary>
public sealed record ProjectCompletedNotification(int ProjectId) : INotification;
