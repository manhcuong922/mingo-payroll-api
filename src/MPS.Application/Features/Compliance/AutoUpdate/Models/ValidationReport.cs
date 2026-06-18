namespace MPS.Application.Features.Compliance.AutoUpdate.Models;

public enum ValidationSeverity { Pass, Warning, Error }

public sealed record ValidationItem(
    string Layer,      // "Grounding" | "Logic" | "Diff"
    ValidationSeverity Severity,
    string Field,
    string Message);

/// <summary>Gom kết quả 3 lớp validation.</summary>
public sealed class ValidationReport
{
    public List<ValidationItem> Items { get; } = new();

    public void Pass(string layer, string field, string message)
        => Items.Add(new ValidationItem(layer, ValidationSeverity.Pass, field, message));

    public void Warn(string layer, string field, string message)
        => Items.Add(new ValidationItem(layer, ValidationSeverity.Warning, field, message));

    public void Error(string layer, string field, string message)
        => Items.Add(new ValidationItem(layer, ValidationSeverity.Error, field, message));

    public int WarningCount => Items.Count(i => i.Severity == ValidationSeverity.Warning);
    public int ErrorCount => Items.Count(i => i.Severity == ValidationSeverity.Error);
    public bool HasError => ErrorCount > 0;
}
