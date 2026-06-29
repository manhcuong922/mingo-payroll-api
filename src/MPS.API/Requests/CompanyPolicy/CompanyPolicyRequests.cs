using Microsoft.AspNetCore.Http;

namespace MPS.API.Requests.CompanyPolicy;

public sealed class CreateCompanyPolicyRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public IFormFile File { get; set; } = null!;
}

public sealed class UpdateCompanyPolicyRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UploadCompanyPolicyVersionRequest
{
    public string? Notes { get; set; }
    public IFormFile File { get; set; } = null!;
}
