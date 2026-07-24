namespace MPS.Application.DTOs;

/// <summary>
/// Một mốc lương của hợp đồng: từ <paramref name="EffectiveFrom"/> trở đi thì bảng lương dùng
/// <paramref name="SalaryAmount"/>. Mốc đầu tiên là lần ký hợp đồng (<c>AppendixId = null</c>),
/// các mốc sau đến từ phụ lục đổi lương.
/// </summary>
public sealed record ContractSalarySnapshotDto(
    DateTime EffectiveFrom,
    decimal SalaryAmount,
    string Currency,
    int? AppendixId,
    string? AppendixNo);
