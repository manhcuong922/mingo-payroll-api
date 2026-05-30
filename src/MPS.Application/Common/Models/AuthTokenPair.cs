namespace MPS.Application.Common.Models;

public sealed record AuthTokenPair(
    AccessTokenResult AccessToken,
    RefreshTokenResult RefreshToken);
