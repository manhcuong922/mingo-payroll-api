using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MPS.API.Requests.Contract;

/// <summary>
/// multipart/form-data payload for POST /api/contracts/{id}/activate.
/// </summary>
public sealed class ActivateContractRequest
{
    [FromForm(Name = "file")]
    public IFormFile File { get; set; } = default!;

    [FromForm(Name = "signedAt")]
    public DateTime SignedAt { get; set; }

    [FromForm(Name = "signedPlace")]
    public string SignedPlace { get; set; } = string.Empty;

    [FromForm(Name = "signedBySource")]
    public string? SignedBySource { get; set; }
}
