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
    public void Validate_Passes_WhenAmountWrittenInMillions()
    {
        // Luật thuế TNCN viết "15,5 triệu đồng/tháng", hệ thống lưu 15500000.
        const string source =
            "a) Mức giảm trừ đối với người nộp thuế là 15,5 triệu đồng/tháng (186 triệu đồng/năm);";
        var result = new ExtractionResult
        {
            Values =
            {
                new ExtractedValue
                {
                    Field = "PersonalDeductionAmount",
                    Number = 15_500_000m,
                    Evidence = "người nộp thuế là 15,5 triệu đồng/tháng",
                },
            },
        };
        var report = new ValidationReport();

        _sut.Validate(result, source, report);

        report.HasError.Should().BeFalse();
    }

    [Fact]
    public void Validate_Passes_WhenRateStoredAsDecimalButWrittenAsPercent()
    {
        // Bảng biểu thuế ghi cột thuế suất là "5", hệ thống lưu 0.05.
        const string source = "| 1 | Đến 120 | Đến 10 | 5 |";
        var result = new ExtractionResult
        {
            Values = { new ExtractedValue { Field = "Bracket:1:Rate", Number = 0.05m, Evidence = source } },
        };
        var report = new ValidationReport();

        _sut.Validate(result, source, report);

        report.HasError.Should().BeFalse();
    }

    [Fact]
    public void Validate_Passes_WhenSourceWritesLeadingZero()
    {
        // Bộ luật Lao động viết "08 giờ", "48 giờ" — số 0 đứng đầu không được coi là lệch.
        const string source = "Thời giờ làm việc bình thường không quá 08 giờ trong 01 ngày.";
        var result = new ExtractionResult
        {
            Values =
            {
                new ExtractedValue
                {
                    Field = "StandardWorkingHoursPerDay",
                    Number = 8m,
                    Evidence = "không quá 08 giờ trong 01 ngày",
                },
            },
        };
        var report = new ValidationReport();

        _sut.Validate(result, source, report);

        report.HasError.Should().BeFalse();
    }

    [Fact]
    public void Validate_Errors_WhenRateOffByFactorOfTen()
    {
        // 0.5 (50%) không được coi là khớp với cột ghi "5".
        const string source = "| 1 | Đến 120 | Đến 10 | 5 |";
        var result = new ExtractionResult
        {
            Values = { new ExtractedValue { Field = "Bracket:1:Rate", Number = 0.5m, Evidence = source } },
        };
        var report = new ValidationReport();

        _sut.Validate(result, source, report);

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
