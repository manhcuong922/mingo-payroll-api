using MPS.Domain.Enums;

namespace MPS.API.Requests.Comment;

public record CreateCommentRequest(CommentOwnerType OwnerType, int OwnerId, string Content);
