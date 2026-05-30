namespace MPS.API.Requests.Auth;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
