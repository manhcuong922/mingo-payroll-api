using MediatR;
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
    string? Notes) : IRequest<int>;
