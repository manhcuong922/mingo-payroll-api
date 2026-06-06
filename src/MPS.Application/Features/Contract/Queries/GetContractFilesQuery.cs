using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Contract.Queries;

/// <summary>
/// Trả về tất cả file PDF của 1 HĐ (TargetType = Contract), sort Version DESC.
/// Mỗi file kèm presigned download URL TTL 15 phút.
/// </summary>
public sealed record GetContractFilesQuery(int ContractId) : IRequest<IReadOnlyList<ContractFileDto>>;
