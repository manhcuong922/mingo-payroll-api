namespace MPS.Application.DTOs;

public sealed record ContractPartyDto(
    int Id,
    string LegalName,
    string TaxCode,
    string Address,
    string RepresentativeFullName,
    string RepresentativeTitle,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    DateTime CreatedAt);
