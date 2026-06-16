namespace MPS.API.Requests.Position;

public sealed record UpdatePositionRequest(string Code, string Name, string? Description);
