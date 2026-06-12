namespace MPS.Application.Common.Options;

public sealed class FptAiOcrOptions
{
    public const string SectionName = "FptAiOcr";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.fpt.ai";
    public int TimeoutSeconds { get; init; } = 15;
    public long MaxImageSizeBytes { get; init; } = 5_242_880;
}
