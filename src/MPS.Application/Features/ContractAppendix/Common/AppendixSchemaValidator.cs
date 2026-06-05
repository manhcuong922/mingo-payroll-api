using System.Text.Json;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.ContractAppendix.Common;

/// <summary>
/// Validate <c>ContractAppendix.ChangedClausesJson</c> ở 2 tầng:
/// 1. JSON cú pháp hợp lệ + deserialize được sang <see cref="AppendixChangeSet"/>
/// 2. Chặn các key bị cấm theo Điều 22 k.2 (thời hạn + loại HĐ) — kể cả khi FE gửi
///    key dưới dạng tên-camelCase hay PascalCase.
/// </summary>
public static class AppendixSchemaValidator
{
    /// <summary>Các key tuyệt đối không được phép xuất hiện trong ChangedClausesJson.</summary>
    public static readonly IReadOnlySet<string> ForbiddenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "EffectiveFrom",
        "EffectiveTo",
        "ContractCategory",
        "ContractSubtype",
    };

    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parse + validate. Throw <see cref="DomainValidationException"/> nếu vi phạm.
    /// </summary>
    public static AppendixChangeSet ParseAndValidate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new DomainValidationException("ChangedClausesJson is required.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new DomainValidationException($"ChangedClausesJson is not valid JSON: {ex.Message}");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new DomainValidationException("ChangedClausesJson must be a JSON object.");

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (ForbiddenKeys.Contains(prop.Name))
                    throw new DomainValidationException(
                        $"FORBIDDEN_APPENDIX_KEY: '{prop.Name}' cannot be changed via appendix — Điều 22 k.2 BLLĐ 2019.");
            }
        }

        AppendixChangeSet? changeSet;
        try
        {
            changeSet = JsonSerializer.Deserialize<AppendixChangeSet>(json, s_options);
        }
        catch (JsonException ex)
        {
            throw new DomainValidationException($"ChangedClausesJson schema mismatch: {ex.Message}");
        }

        if (changeSet is null)
            throw new DomainValidationException("ChangedClausesJson deserialized to null.");

        if (HasNoEffectiveChange(changeSet))
            throw new DomainValidationException("ChangedClausesJson does not contain any actionable change.");

        return changeSet;
    }

    private static bool HasNoEffectiveChange(AppendixChangeSet c) =>
        c.JobDescription is null
        && c.WorkLocation is null
        && c.SalaryGradeId is null
        && c.PromotionPolicy is null
        && c.PpePolicy is null
        && c.TrainingPolicy is null
        && c.ConfidentialityClause is null
        && c.WorkingTimeJson is null
        && c.AddAllowances.Count == 0
        && c.UpdateAllowances.Count == 0
        && c.RemoveAllowanceTypeIds.Count == 0;
}
