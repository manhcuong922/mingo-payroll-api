using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.ContractAppendix.Queries;

/// <summary>
/// Trả về tất cả file của 1 phụ lục (TargetType = Appendix, TargetId = appendixId),
/// sort Version DESC. Mỗi file kèm presigned download URL TTL 15 phút.
/// </summary>
public sealed record GetAppendixFilesQuery(int AppendixId) : IRequest<IReadOnlyList<ContractFileDto>>;
