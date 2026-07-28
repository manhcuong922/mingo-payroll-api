using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Contract.Queries;

/// <summary>
/// [D12] — Lịch sử mức lương của một hợp đồng theo mốc hiệu lực: mốc gốc lúc ký, cộng thêm mỗi
/// phụ lục đã kích hoạt có đổi lương. Sắp xếp tăng dần theo ngày hiệu lực.
///
/// Vì sao cần lộ ra API: kế toán/nhân sự phải giải thích được "kỳ tháng 5 trả theo mức nào" khi
/// người lao động thắc mắc, và đây cũng chính là dữ liệu bảng lương dùng để tính lại kỳ cũ.
/// </summary>
public sealed record GetContractSalarySnapshotsQuery(int ContractId)
    : IRequest<IReadOnlyList<ContractSalarySnapshotDto>>;
