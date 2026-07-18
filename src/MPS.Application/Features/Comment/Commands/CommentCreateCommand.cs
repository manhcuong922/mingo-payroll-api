using MediatR;
using MPS.Domain.Enums;

namespace MPS.Application.Features.Comment.Commands;

public record CommentCreateCommand(
    CommentOwnerType OwnerType,
    int OwnerId,
    string Content) : IRequest<int>;
