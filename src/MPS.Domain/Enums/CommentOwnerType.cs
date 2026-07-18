namespace MPS.Domain.Enums;

// Comment gắn vào Đơn (Project) hoặc Task (dùng chung cho cả Công đoạn và Task lá,
// phân biệt bằng ProjectTask.IsBatchParent — plan mục 5).
public enum CommentOwnerType
{
    Project,
    Task,
}
