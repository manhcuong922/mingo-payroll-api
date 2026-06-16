namespace MPS.API.Requests.Position;

public sealed record CreatePositionRequest(string Code, string Name, string? Description);
