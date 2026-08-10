using FluentAssertions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Infrastructure.ComplianceAutoUpdate.Ai;

namespace MPS.Infrastructure.Tests.ComplianceAutoUpdate;

/// <summary>
/// Chốt hồi quy cho đúng con bug thật: cắt cứng scopedText[..MaxInputChars] từng nuốt mất Điều 9/10
/// của Luật thuế TNCN (biểu thuế + giảm trừ gia cảnh) ⇒ AI trả values rỗng ⇒ proposal rác.
/// </summary>
public sealed class ExtractionBatcherTests
{
    private static string Article(int n, string body) => $"Điều {n}. {body}";

    [Fact]
    public void Split_ReturnsWholeText_WhenShorterThanLimit()
    {
        var text = Article(1, "Phạm vi điều chỉnh.");

        var batches = ExtractionBatcher.Split(text, maxChars: 1000, maxBatches: 6);

        batches.Should().ContainSingle().Which.Should().Be(text);
    }

    [Fact]
    public void Split_KeepsEveryArticle_WhenTextExceedsLimit()
    {
        // 5 điều × ~120 ký tự, giới hạn 250 ⇒ phải chia lô chứ không được vứt điều nào.
        var text = string.Join("\n\n", Enumerable.Range(1, 5)
            .Select(i => Article(i, new string('x', 100) + $" giá trị {i}")));

        var batches = ExtractionBatcher.Split(text, maxChars: 250, maxBatches: 6);

        batches.Count.Should().BeGreaterThan(1);
        var rejoined = string.Join("\n\n", batches);
        for (var i = 1; i <= 5; i++)
            rejoined.Should().Contain($"Điều {i}.", $"điều {i} không được rơi mất khi chia lô");
    }

    [Fact]
    public void Split_NeverCutsInsideAnArticle_WhenArticleFitsInABatch()
    {
        var text = string.Join("\n\n", new[]
        {
            Article(1, new string('a', 200)),
            Article(2, new string('b', 200)),
        });

        var batches = ExtractionBatcher.Split(text, maxChars: 300, maxBatches: 6);

        batches.Should().HaveCount(2);
        batches.Should().OnlyContain(b => b.StartsWith("Điều "));
    }

    [Fact]
    public void Split_StopsAtMaxBatches()
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 20)
            .Select(i => Article(i, new string('x', 200))));

        var batches = ExtractionBatcher.Split(text, maxChars: 250, maxBatches: 3);

        batches.Should().HaveCount(3);
    }

    [Fact]
    public void Merge_UnionsFieldsAcrossBatches_AndKeepsFirstNumericHit()
    {
        var parts = new[]
        {
            new ExtractionResult
            {
                TargetEntityType = TargetEntityTypes.PitRegulationVersion,
                Values = { new ExtractedValue { Field = "PersonalDeductionAmount", Number = 15_500_000m } },
            },
            new ExtractionResult
            {
                DocumentNo = "109/2025/QH15",
                EffectiveDate = new DateOnly(2026, 1, 1),
                TargetEntityType = TargetEntityTypes.PitRegulationVersion,
                Values =
                {
                    new ExtractedValue { Field = "PersonalDeductionAmount", Number = 11_000_000m },
                    new ExtractedValue { Field = "Bracket:1:Rate", Number = 0.05m },
                },
            },
        };

        var merged = ExtractionBatcher.Merge(parts);

        merged.DocumentNo.Should().Be("109/2025/QH15");
        merged.EffectiveDate.Should().Be(new DateOnly(2026, 1, 1));
        merged.Values.Should().HaveCount(2);
        merged.Values.Single(v => v.Field == "PersonalDeductionAmount").Number.Should().Be(15_500_000m);
    }

    [Fact]
    public void Merge_PicksMajorityEntityType_WhenOneBatchGoesOffTopic()
    {
        var parts = new[]
        {
            new ExtractionResult { TargetEntityType = TargetEntityTypes.PitRegulationVersion },
            new ExtractionResult { TargetEntityType = TargetEntityTypes.AllowanceExemptThresholdVersion },
            new ExtractionResult { TargetEntityType = TargetEntityTypes.PitRegulationVersion },
        };

        ExtractionBatcher.Merge(parts).TargetEntityType
            .Should().Be(TargetEntityTypes.PitRegulationVersion);
    }

    [Fact]
    public void Merge_SkipsValuesWithNeitherNumberNorText()
    {
        var parts = new[]
        {
            new ExtractionResult
            {
                TargetEntityType = TargetEntityTypes.MinimumWageVersion,
                Values =
                {
                    new ExtractedValue { Field = "Region:I", Number = null, Text = null },
                    new ExtractedValue { Field = "Region:II", Number = 4_410_000m },
                },
            },
            new ExtractionResult
            {
                TargetEntityType = TargetEntityTypes.MinimumWageVersion,
                Values = { new ExtractedValue { Field = "Region:I", Number = 4_960_000m } },
            },
        };

        var merged = ExtractionBatcher.Merge(parts);

        merged.Values.Should().HaveCount(2);
        merged.Values.Single(v => v.Field == "Region:I").Number.Should().Be(4_960_000m);
    }
}
