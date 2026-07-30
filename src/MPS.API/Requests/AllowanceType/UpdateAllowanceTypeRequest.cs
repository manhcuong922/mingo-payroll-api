namespace MPS.API.Requests.AllowanceType;

public sealed record UpdateAllowanceTypeRequest(
    string Code,
    string Name,
    string? Description,
    decimal? DefaultAmount,
    bool IsBhxhBase,
    bool IsTaxable,
    // PRORATE-02 — mặc định false: phụ cấp chia theo ngày công như `[D20]`; bật để trả nguyên khoản.
    bool ExcludeFromProration = false,
    bool IsActive = true);
