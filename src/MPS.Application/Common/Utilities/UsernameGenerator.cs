using System.Globalization;
using System.Text;

namespace MPS.Application.Common.Utilities;

/// Generates a username from a Vietnamese full name + employee code.
/// Rule: take the last name token (given name) lowercased + diacritic-stripped,
/// then append the first letter of each remaining token, then append the employee code.
/// Example: "Vũ Đình Thắng" + "BA260001" → "thangvdBA260001".
/// Example: "Lê Thị Thu Vân" + "BA260001" → "vanlttBA260001".
public static class UsernameGenerator
{
    public static string Generate(string fullName, string employeeCode)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("FullName must not be empty.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new ArgumentException("EmployeeCode must not be empty.", nameof(employeeCode));

        var tokens = fullName
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
            throw new ArgumentException("FullName must contain at least one token.", nameof(fullName));

        var givenName = StripDiacritics(tokens[^1]).ToLowerInvariant();

        var initials = new StringBuilder();
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            var token = StripDiacritics(tokens[i]);
            if (token.Length > 0)
                initials.Append(char.ToLowerInvariant(token[0]));
        }

        return string.Concat(givenName, initials.ToString(), employeeCode);
    }

    private static string StripDiacritics(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;

            // Map đ/Đ to d/D (NFD does not decompose these on its own)
            switch (ch)
            {
                case 'đ': sb.Append('d'); break;
                case 'Đ': sb.Append('D'); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
