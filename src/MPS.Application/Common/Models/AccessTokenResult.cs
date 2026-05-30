namespace MPS.Application.Common.Models;

public sealed record AccessTokenResult(
    string Token,
    DateTime ExpiresAt,
    string Jti);
