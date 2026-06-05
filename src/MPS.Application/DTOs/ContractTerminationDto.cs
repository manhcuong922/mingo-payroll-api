namespace MPS.Application.DTOs;

public sealed record ContractTerminationDto(
    int Id,
    int ContractId,
    DateTime TerminationDate,
    int ReasonRefId,
    string ReasonCode,
    string ReasonDescription,
    string TerminationParty,
    string LegalBasis,
    DateTime? NoticeSentAt,
    int? NoticeDaysAhead,
    bool NoNoticeRequired,
    int? WrittenNoticeFileId,
    int DecidedByUserId,
    string? Notes,
    DateTime CreatedAt);
