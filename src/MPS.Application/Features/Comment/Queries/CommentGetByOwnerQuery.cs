using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Enums;

namespace MPS.Application.Features.Comment.Queries;

public record CommentGetByOwnerQuery(CommentOwnerType OwnerType, int OwnerId) : IRequest<IReadOnlyList<CommentDto>>;
