namespace MPS.Domain.Enums;

/// <summary>
/// Loại email trong outbox — quyết định cách worker dựng nội dung khi gửi.
/// </summary>
public enum EmailOutboxType : byte
{
    PasswordReset = 1,
    AccountCredentials = 2,
    ContractActivation = 3,
}
