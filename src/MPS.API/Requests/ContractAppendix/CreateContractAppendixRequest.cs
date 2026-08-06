namespace MPS.API.Requests.ContractAppendix;

/// <summary>
/// FE chỉ gửi các trường nội dung — <c>AppendixNo</c> BE sinh theo mẫu
/// <c>{ContractNo}-PL-{seq:D2}</c>, <c>AppendixType</c> BE suy từ nội dung
/// (Amendment nếu đổi position/salary/allowance, Detail nếu chỉ mô tả).
/// </summary>
public sealed class CreateContractAppendixRequest
{
    public string Title { get; set; } = string.Empty;
    public string ChangedClausesJson { get; set; } = string.Empty;
    public string AppendixText { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
}
