namespace MPS.Application.Common.Models;

public sealed record RefreshTokenResult(
    string Token,
    DateTime ExpiresAt);
