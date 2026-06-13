using System.ComponentModel.DataAnnotations;

namespace MPS.Application.Options;

public sealed class ComplianceOptions
{
    public const string SectionName = "Compliance";

    [Range(1, 3650)]
    public int MaxForwardDays { get; init; } = 365;
}
