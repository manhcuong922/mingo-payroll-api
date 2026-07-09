using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

/// <summary>
/// Validate gán người vào 1 vị trí lá (refactor I/P/D) — dùng chung cho TaskCreate + TaskUpdate:
///   - assignee phải thuộc xưởng của task;
///   - phải ĐÚNG chức danh RequiredPositionId (nếu vị trí yêu cầu) → sai chức danh ném 400;
///   - người MỚI được thêm không được đang có task InProgress ở nơi khác (nới luật "1 task mở").
/// Số lượng vs PlannedHeadcount chỉ cảnh báo mềm ở FE — KHÔNG chặn ở đây.
/// </summary>
internal static class TaskAssigneeGuard
{
    public static async Task ValidateAsync(
        IReadOnlyList<int> assigneeUserIds,
        int departmentId,
        int? requiredPositionId,
        IReadOnlyCollection<int> alreadyAssignedUserIds,
        IDepartmentRepository departments,
        IProjectTaskRepository tasks,
        CancellationToken ct)
    {
        if (assigneeUserIds.Count == 0) return;

        var members = (await departments.GetMemberUserIdsAsync(departmentId, ct)).ToHashSet();

        foreach (var userId in assigneeUserIds.Distinct())
        {
            if (!members.Contains(userId))
                throw new DomainValidationException("Có người được giao không thuộc xưởng của task.");

            if (requiredPositionId is int positionId
                && !await departments.IsUserInDepartmentWithPositionAsync(userId, departmentId, positionId, ct))
            {
                throw new DomainValidationException(
                    $"Nhân viên #{userId} không đúng chức danh yêu cầu của vị trí này.");
            }

            if (alreadyAssignedUserIds.Contains(userId)) continue;

            if (await tasks.CountInProgressByUserAsync(userId, ct) > 0)
                throw new DomainValidationException(
                    $"Nhân viên #{userId} đang có task InProgress, không thể giao thêm task mới.");
        }
    }
}
