using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Infrastructure.ComplianceAutoUpdate.Validation;

namespace MPS.Infrastructure.Tests.ComplianceAutoUpdate;

public sealed class SourceGroundingValidatorTests
{
    private readonly SourceGroundingValidator _sut = new(NullLogger<SourceGroundingValidator>.Instance);

    [Fact]
    public void Validate_Passes_WhenEvidenceAndNumberExistInSource()
    {
        // Arrange
        const string source = "Mức lương tối thiểu vùng I là 4.960.000 đồng/tháng.";
        var result = new ExtractionResult
        {
            Values =
            {
                new ExtractedValue { Field = "Region:I", Number = 4_960_000m, Evidence = "vùng I là 4.960.000 đồng" },
            },
        };
        var report = new ValidationReport();

        // Act
        _sut.Validate(result, source, report);

        // Assert
        report.HasError.Should().BeFalse();
    }

    [Fact]
    public void Validate_Errors_WhenEvidenceNotInSource()
    {
        var result = new ExtractionResult
        {
            Values = { new ExtractedValue { Field = "Region:I", Number = 4_960_000m, Evidence = "câu bịa không có" } },
        };
        var report = new ValidationReport();

        _sut.Validate(result, "Văn bản gốc khác hẳn.", report);

        report.HasError.Should().BeTrue();
    }

    [Fact]
    public void Validate_Errors_WhenNumberMisreadEvenIfEvidencePresent()
    {
        // evidence khớp nhưng con số 3.290.000 không có trong văn bản (văn bản ghi 3.920.000)
        const string source = "vùng IV là 3.920.000 đồng";
        var result = new ExtractionResult
        {
            Values = { new ExtractedValue { Field = "Region:IV", Number = 3_290_000m, Evidence = "vùng IV là" } },
        };
        var report = new ValidationReport();

        _sut.Validate(result, source, report);

        report.HasError.Should().BeTrue();
    }
}
