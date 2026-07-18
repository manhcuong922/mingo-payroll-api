namespace MPS.Application.DTOs;

public record CommentDto(
    int Id,
    string OwnerType,
    int OwnerId,
    string Content,
    int AuthorUserId,
    string? AuthorName,
    DateTime CreatedAt);
