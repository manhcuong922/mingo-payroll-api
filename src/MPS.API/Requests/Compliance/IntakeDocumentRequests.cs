namespace MPS.API.Requests.Compliance;

/// <summary>Body cho nạp văn bản qua URL (Kênh A).</summary>
public sealed record IngestDocumentUrlRequest(string Url, string? DocumentNo);
