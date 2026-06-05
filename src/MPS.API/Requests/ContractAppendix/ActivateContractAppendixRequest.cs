using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MPS.API.Requests.ContractAppendix;

public sealed class ActivateContractAppendixRequest
{
    [FromForm(Name = "file")]
    public IFormFile File { get; set; } = default!;

    [FromForm(Name = "signedAt")]
    public DateTime SignedAt { get; set; }

    [FromForm(Name = "signedBySource")]
    public string? SignedBySource { get; set; }
}
