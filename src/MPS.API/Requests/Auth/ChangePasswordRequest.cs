namespace MPS.API.Requests.Auth;

public sealed record ChangePasswordRequest(string OldPassword, string NewPassword);
