namespace MPS.API.Requests.Compliance;

/// <summary>Body tuỳ chọn khi duyệt/từ chối một proposal cập nhật pháp luật.</summary>
public sealed record ReviewProposalRequest(string? Note);
