using MPS.Domain.Attributes;
using MPS.Domain.Enums;

namespace MPS.Domain.Entities;

/// <summary>
/// Comment 2 chiều theo tầng (plan mục 5). Polymorphic OwnerType+OwnerId — KHÔNG FK cứng tới Project/Task,
/// giống pattern <see cref="ProductionAttachment"/>. OwnerType=Task dùng chung cho cả Công đoạn và Task lá
/// (phân biệt qua ProjectTask.IsBatchParent khi cần).
/// </summary>
[Auditable]
public class Comment
{
    public int Id { get; set; }

    public CommentOwnerType OwnerType { get; set; }
    public int OwnerId { get; set; }

    public string Content { get; set; } = string.Empty;

    public int AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }

    public DateTime CreatedAt { get; set; }
}
