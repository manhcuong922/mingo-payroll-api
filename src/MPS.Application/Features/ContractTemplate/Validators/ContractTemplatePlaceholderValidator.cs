using System.Text.RegularExpressions;

namespace MPS.Application.Features.ContractTemplate.Validators;

/// <summary>
/// Validate cú pháp placeholder {{...}} trong TemplateBody.
/// Quy tắc: số lượng "{{" và "}}" cân bằng; không lồng nhau; mỗi placeholder
/// chỉ chứa ký tự an toàn (chữ, số, dấu chấm, gạch dưới).
/// </summary>
internal static class ContractTemplatePlaceholderValidator
{
    private static readonly Regex PlaceholderPattern = new(@"\{\{\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\}\}", RegexOptions.Compiled);

    public static bool IsValid(string? templateBody, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(templateBody))
        {
            return true;
        }

        var opens = CountOccurrences(templateBody, "{{");
        var closes = CountOccurrences(templateBody, "}}");
        if (opens != closes)
        {
            error = $"Unbalanced placeholder delimiters: '{{{{' occurs {opens} time(s) but '}}}}' occurs {closes} time(s).";
            return false;
        }

        // No nested or empty placeholders
        var idx = 0;
        while ((idx = templateBody.IndexOf("{{", idx, StringComparison.Ordinal)) >= 0)
        {
            var end = templateBody.IndexOf("}}", idx + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                error = "Unterminated placeholder: '{{' without matching '}}'.";
                return false;
            }
            var inner = templateBody.Substring(idx + 2, end - idx - 2);
            if (inner.Contains("{{", StringComparison.Ordinal))
            {
                error = "Nested placeholders are not allowed.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(inner))
            {
                error = "Empty placeholder '{{}}' is not allowed.";
                return false;
            }
            if (!PlaceholderPattern.IsMatch("{{" + inner + "}}"))
            {
                error = $"Invalid placeholder syntax: '{{{{ {inner.Trim()} }}}}'. Allowed characters: letters, digits, '.', '_'.";
                return false;
            }
            idx = end + 2;
        }

        return true;
    }

    private static int CountOccurrences(string source, string token)
    {
        var count = 0;
        var idx = 0;
        while ((idx = source.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += token.Length;
        }
        return count;
    }
}
