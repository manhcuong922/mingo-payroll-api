using MPS.Application.Common.Exceptions.Storage;

namespace MPS.Application.Common.Storage;

public static class ObjectKeyBuilder
{
    private const int MaxKeyLength = 1024;

    public static string ForContract(string contractNo, int version) =>
        Build("contracts", DateTime.UtcNow, contractNo, $"v{version}.pdf");

    public static string ForAppendix(string contractNo, string appendixNo, int version)
    {
        ValidateSegment(appendixNo, nameof(appendixNo));
        return Build("appendices", DateTime.UtcNow, contractNo, $"{appendixNo}-v{version}.pdf");
    }

    public static string ForTerminationNotice(string contractNo, int version) =>
        Build("terminations", DateTime.UtcNow, contractNo, $"notice-v{version}.pdf");

    public static string ForAuthorization(Guid authId, int version) =>
        Build("authorizations", DateTime.UtcNow, authId.ToString("N"), $"v{version}.pdf");

    public static string ForPayslip(int employeeId, int year, int month)
    {
        if (year < 2000 || year > 2999) throw new ArgumentOutOfRangeException(nameof(year));
        if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var key = $"payslips/{year}/{month:D2}/{employeeId}.pdf";
        ValidateLength(key);
        return key;
    }

    public static string ForTemplate(int templateId, int version)
    {
        if (templateId <= 0) throw new ArgumentOutOfRangeException(nameof(templateId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        var key = $"templates/{templateId}/v{version}.docx";
        ValidateLength(key);
        return key;
    }

    public static string ForCompanyPolicy(int policyId, int version, string extension)
    {
        if (policyId <= 0) throw new ArgumentOutOfRangeException(nameof(policyId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        var ext = (extension ?? string.Empty).TrimStart('.').ToLowerInvariant();
        if (ext != "pdf" && ext != "docx")
            throw new ArgumentException("Company policy file extension must be 'pdf' or 'docx'.", nameof(extension));
        var key = $"company-policies/{policyId}/v{version}.{ext}";
        ValidateLength(key);
        return key;
    }

    public static string ForEmployeeAvatar(int employeeId, string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
            throw new ArgumentException("File extension is required.", nameof(fileExtension));

        var ext = fileExtension.TrimStart('.').ToLowerInvariant();
        ValidateSegment(ext, nameof(fileExtension));
        var key = $"avatars/{employeeId}.{ext}";
        ValidateLength(key);
        return key;
    }

    public static string ForFacePhoto(int employeeId, Guid registrationId) =>
        $"face-photos/{employeeId}/{registrationId:N}.jpg";

    public static string ForLeaveMedicalCertificate(int employeeId, string contentType, Guid uniqueId)
    {
        if (employeeId <= 0) throw new ArgumentOutOfRangeException(nameof(employeeId));
        var ext = ResolveImageExtension(contentType);
        var now = DateTime.UtcNow;
        var key = $"leave-medical-certificates/{now:yyyy}/{now:MM}/{employeeId}/{uniqueId:N}.{ext}";
        ValidateLength(key);
        return key;
    }

    /// <summary>
    /// Object key ảnh evidence cho nghĩa vụ BHXH/TNCN. Kind = "bank-receipt" hoặc "authority-confirmation".
    /// Path: <c>payroll/bhxh-tax-evidence/{obligationId}/{kind}/{guid}.{ext}</c>.
    /// </summary>
    public static string ForBhxhTaxEvidence(int obligationId, string kind, string contentType, Guid uniqueId)
    {
        if (obligationId <= 0) throw new ArgumentOutOfRangeException(nameof(obligationId));
        ValidateSegment(kind, nameof(kind));
        var ext = ResolveImageExtension(contentType);
        var key = $"payroll/bhxh-tax-evidence/{obligationId}/{kind}/{uniqueId:N}.{ext}";
        ValidateLength(key);
        return key;
    }

    private static string ResolveImageExtension(string contentType)
    {
        var normalized = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "image/jpeg" or "image/jpg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => throw new ArgumentException(
                $"Unsupported medical certificate content type '{contentType}'. Allowed: image/jpeg, image/png, image/webp.",
                nameof(contentType)),
        };
    }

    public static void EnsureValid(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new InvalidObjectKeyException(objectKey ?? string.Empty, "Object key cannot be empty.");

        if (objectKey.Length > MaxKeyLength)
            throw new InvalidObjectKeyException(objectKey, $"Object key exceeds {MaxKeyLength} characters.");

        if (objectKey.Contains("..", StringComparison.Ordinal))
            throw new InvalidObjectKeyException(objectKey, "Object key cannot contain '..'.");

        if (objectKey.Contains('\0'))
            throw new InvalidObjectKeyException(objectKey, "Object key cannot contain null bytes.");

        if (objectKey.StartsWith('/') || objectKey.StartsWith('\\'))
            throw new InvalidObjectKeyException(objectKey, "Object key cannot start with a slash.");

        if (objectKey.Contains('\\'))
            throw new InvalidObjectKeyException(objectKey, "Object key cannot contain backslashes.");
    }

    private static string Build(string module, DateTime when, string refKey, string fileName)
    {
        ValidateSegment(refKey, nameof(refKey));
        ValidateSegment(fileName, nameof(fileName));
        var key = $"{module}/{when:yyyy}/{when:MM}/{refKey}/{fileName}";
        ValidateLength(key);
        return key;
    }

    private static void ValidateSegment(string segment, string paramName)
    {
        if (string.IsNullOrWhiteSpace(segment))
            throw new ArgumentException("Segment cannot be empty.", paramName);

        if (segment.Contains("..", StringComparison.Ordinal) ||
            segment.Contains('\0') ||
            segment.Contains('/') ||
            segment.Contains('\\'))
        {
            throw new InvalidObjectKeyException(segment,
                $"Segment '{paramName}' contains forbidden characters.");
        }
    }

    private static void ValidateLength(string key)
    {
        if (key.Length > MaxKeyLength)
            throw new InvalidObjectKeyException(key, $"Object key exceeds {MaxKeyLength} characters.");
    }
}
