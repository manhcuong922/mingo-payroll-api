using MPS.Domain.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

/// <summary>
/// Validate gán người vào 1 vị trí lá (refactor I/P/D) — dùng chung cho TaskCreate + TaskUpdate:
///   - assignee phải thuộc xưởng của task;
///   - phải ĐÚNG chức danh RequiredPositionId (nếu vị trí yêu cầu) → sai chức danh ném 400;
///   - phải còn QUAN HỆ LAO ĐỘNG (HĐ Active, còn hiệu lực tại thời điểm giao) — lỗi #28;
///   - người MỚI được thêm không được đang giữ công đoạn nào CHƯA XONG (Todo/InProgress) — lỗi #24.
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
        IContractRepository contracts,
        CancellationToken ct)
    {
        if (assigneeUserIds.Count == 0) return;

        var distinct = assigneeUserIds.Distinct().ToList();
        var members = (await departments.GetMemberUserIdsAsync(departmentId, ct)).ToHashSet();

        // #28 — chỉ giao việc cho người còn HĐ hiệu lực. Truy vấn 1 lần cho cả danh sách (tránh N+1).
        var employed = (await contracts.GetUserIdsWithActiveContractAsync(distinct, VietnamClock.Now, ct))
            .ToHashSet();

        foreach (var userId in distinct)
        {
            if (!members.Contains(userId))
                throw new DomainValidationException("Có người được giao không thuộc xưởng của task.");

            if (requiredPositionId is int positionId
                && !await departments.IsUserInDepartmentWithPositionAsync(userId, departmentId, positionId, ct))
            {
                throw new DomainValidationException(
                    $"Nhân viên #{userId} không đúng chức danh yêu cầu của vị trí này.");
            }

            if (!employed.Contains(userId))
                throw new DomainValidationException(
                    $"Nhân viên #{userId} không có hợp đồng lao động hiệu lực tại thời điểm giao việc — không thể giao task.");

            if (alreadyAssignedUserIds.Contains(userId)) continue;

            // #24 — chống giao việc chồng: đang giữ công đoạn chưa hoàn thành thì không nhận thêm.
            if (await tasks.CountOpenByUserAsync(userId, ct) > 0)
                throw new DomainValidationException(
                    $"Nhân viên #{userId} đang có công đoạn chưa hoàn thành (Todo/InProgress), không thể giao thêm task mới.");
        }
    }
}
