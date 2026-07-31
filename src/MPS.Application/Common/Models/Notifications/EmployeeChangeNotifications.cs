using MediatR;

namespace MPS.Application.Common.Models.Notifications;

/// <summary>
/// Phase 6 §N12 — field nhạy cảm của NV vừa bị chỉnh (CCCD/STK/TaxNo/SocialNo/Dependents).
/// <see cref="ChangedFields"/> = danh sách tên field đã đổi.
/// </summary>
public sealed record EmployeeSensitiveEditedNotification(
    int EmployeeInformationId,
    int? EmployeeUserId,
    int ChangedByUserId,
    IReadOnlyList<string> ChangedFields) : INotification;

/// <summary>Phase 6 — Đổi bậc lương (SalaryGrade) → NV + Kế toán + quản lý.</summary>
public sealed record SalaryGradeChangedNotification(
    int EmployeeInformationId,
    int? EmployeeUserId,
    int OldSalaryGradeId,
    int NewSalaryGradeId,
    decimal OldBaseSalary,
    decimal NewBaseSalary,
    string Currency,
    int ChangedByUserId) : INotification;

/// <summary>Phase 6 — User được @mention trong Comment.</summary>
public sealed record CommentMentionNotification(
    int CommentId,
    int MentionedUserId,
    int MentionedByUserId,
    string CommentSnippet,
    string LinkUrl) : INotification;
