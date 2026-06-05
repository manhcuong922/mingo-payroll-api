using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MPS.API.Requests.Contract;

/// <summary>
/// multipart/form-data payload for POST /api/contracts/{id}/files/replace.
/// </summary>
public sealed class ReplaceContractFileRequest
{
    [FromForm(Name = "file")]
    public IFormFile File { get; set; } = default!;

    [FromForm(Name = "signedBySource")]
    public string? SignedBySource { get; set; }
}
