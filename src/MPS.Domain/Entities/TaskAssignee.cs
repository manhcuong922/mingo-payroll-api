namespace MPS.Domain.Entities;

/// <summary>
/// Bảng nối multi-assignee (v5): 1 task giao cho 1 HOẶC nhiều người, chung 1 Done.
/// Task 1 người = 1 dòng; task nhóm (vd 5 người đóng 40 kiện) = nhiều dòng.
/// Task cha (IsBatchParent) không có dòng nào.
/// </summary>
public class TaskAssignee
{
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }

    public int UserId { get; set; }          // người được giao
    public User? User { get; set; }

    public DateTime AssignedAt { get; set; }
}
