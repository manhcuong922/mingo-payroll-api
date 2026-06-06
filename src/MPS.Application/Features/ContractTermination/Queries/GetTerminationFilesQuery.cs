using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.ContractTermination.Queries;

/// <summary>
/// Trả về tất cả file thông báo chấm dứt của 1 HĐ (TargetType = Termination, TargetId = contractId),
/// sort Version DESC. Mỗi file kèm presigned download URL TTL 15 phút.
/// </summary>
public sealed record GetTerminationFilesQuery(int ContractId) : IRequest<IReadOnlyList<ContractFileDto>>;
