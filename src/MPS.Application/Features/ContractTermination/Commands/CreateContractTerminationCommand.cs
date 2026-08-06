using MediatR;
using MPS.Application.Common.Interfaces;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractTermination.Commands;

/// <summary>
/// Phase 8 — Tạo bản ghi chấm dứt HĐLĐ + chuyển HĐ sang Status=Terminated trong cùng tx.
/// Validate notice rules (Điều 35/36) + check WrittenNoticeFileId nếu lý do yêu cầu (Điều 45).
/// </summary>
public sealed record CreateContractTerminationCommand(
    int ContractId,
    int ReasonRefId,
    ContractEnums.TerminationParty TerminationParty,
    DateTime TerminationDate,
    DateTime NoticeSentAt,
    int? NoticeDaysAhead,
    bool NoNoticeRequired,
    int? WrittenNoticeFileId,
    string LegalBasis,
    string? Notes) : IRequest<CreateContractTerminationResult>, IContractScopedCommand;

/// <summary>
/// Kết quả chấm dứt HĐ.
/// <para>
/// <paramref name="AppliedStatus"/> = trạng thái mà HĐ vừa chuyển sang:
/// <c>Terminated</c> (TerminationDate đã qua ngày làm cuối) hoặc
/// <c>PendingTermination</c> (còn trong khoảng đợi — job sẽ tự flip sau ngày TerminationDate theo giờ VN).
/// FE dùng để chọn thông báo hiển thị.
/// </para>
/// <para>
/// <paramref name="OpenTasks"/> là CẢNH BÁO (#28): NV vẫn còn công đoạn chưa hoàn thành đứng tên
/// mình tại thời điểm chấm dứt — HR phải sắp xếp bàn giao. Không chặn thao tác
/// (quyết định nghiệp vụ 2026-07-21), chỉ phơi ra để FE hiển thị.
/// </para>
/// </summary>
public sealed record CreateContractTerminationResult(
    int Id,
    ContractEnums.ContractStatus AppliedStatus,
    IReadOnlyList<OpenTaskWarning> OpenTasks);

public sealed record OpenTaskWarning(int TaskId, string Title, string Status, int? ProjectId);
