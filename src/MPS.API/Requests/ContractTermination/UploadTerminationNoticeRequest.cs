using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MPS.API.Requests.ContractTermination;

public sealed class UploadTerminationNoticeRequest
{
    [FromForm(Name = "file")]
    public IFormFile File { get; set; } = default!;

    [FromForm(Name = "signedBySource")]
    public string? SignedBySource { get; set; }
}
