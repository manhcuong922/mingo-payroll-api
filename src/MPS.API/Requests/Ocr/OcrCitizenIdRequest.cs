namespace MPS.API.Requests.Ocr;

public sealed class OcrCitizenIdRequest
{
    public IFormFile Image { get; set; } = null!;
}
