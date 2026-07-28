using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MPS.API.RateLimiting;
using MPS.API.Authorization;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;

namespace MPS.API.Controllers;

[ApiController]
[Route("api/ocr")]
[Authorize]
public class OcrController : ControllerBase
{
    private const long MaxImageSizeBytes = 5_242_880;

    private static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png"
    };

    // JPEG: FF D8 FF  |  PNG: 89 50 4E 47
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    private readonly ICitizenIdOcrService _ocrService;

    public OcrController(ICitizenIdOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    [HttpPost("citizen-id")]
    [HasPermission(Permissions.ContractCreateAll)]
    [RequestSizeLimit(5_242_880)]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(RateLimitPolicies.Ocr)]
    public async Task<IActionResult> RecognizeCitizenId(IFormFile image, CancellationToken cancellationToken)
    {
        var metadataError = ValidateImageMetadata(image);
        if (metadataError is not null) return metadataError;

        await using var stream = image.OpenReadStream();
        if (!await HasValidMagicBytesAsync(stream, cancellationToken))
            return BadRequest(new { error = "INVALID_IMAGE_CONTENT: File content does not match a valid JPEG or PNG." });

        return await ExecuteOcrAsync(() => _ocrService.RecognizeAsync(stream, image.ContentType, cancellationToken));
    }

    [HttpPost("citizen-id-back")]
    [HasPermission(Permissions.ContractCreateAll)]
    [RequestSizeLimit(5_242_880)]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(RateLimitPolicies.Ocr)]
    public async Task<IActionResult> RecognizeCitizenIdBack(IFormFile image, CancellationToken cancellationToken)
    {
        var metadataError = ValidateImageMetadata(image);
        if (metadataError is not null) return metadataError;

        await using var stream = image.OpenReadStream();
        if (!await HasValidMagicBytesAsync(stream, cancellationToken))
            return BadRequest(new { error = "INVALID_IMAGE_CONTENT: File content does not match a valid JPEG or PNG." });

        return await ExecuteOcrAsync(() => _ocrService.RecognizeBackAsync(stream, image.ContentType, cancellationToken));
    }

    private IActionResult? ValidateImageMetadata(IFormFile image)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = "IMAGE_REQUIRED" });

        if (!AllowedContentTypes.Contains(image.ContentType))
            return BadRequest(new { error = "INVALID_IMAGE_TYPE: Only JPEG and PNG are accepted." });

        if (image.Length > MaxImageSizeBytes)
            return BadRequest(new { error = "IMAGE_TOO_LARGE: Maximum image size is 5MB." });

        return null;
    }

    private async Task<IActionResult> ExecuteOcrAsync<T>(Func<Task<T>> recognize)
    {
        try
        {
            return Ok(await recognize());
        }
        catch (OcrInvalidDocumentTypeException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (OcrFailedException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (OcrUnavailableException)
        {
            return StatusCode(503, new { error = "OCR_UNAVAILABLE: OCR service is temporarily unavailable." });
        }
    }

    private static async Task<bool> HasValidMagicBytesAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        var read = await stream.ReadAsync(header, ct);
        stream.Seek(0, SeekOrigin.Begin);

        if (read >= 3 && header.Take(3).SequenceEqual(JpegMagic)) return true;
        if (read >= 4 && header.Take(4).SequenceEqual(PngMagic)) return true;
        return false;
    }
}
