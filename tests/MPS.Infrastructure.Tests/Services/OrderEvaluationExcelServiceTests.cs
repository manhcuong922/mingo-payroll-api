using FluentAssertions;
using MPS.Application.DTOs.Evaluation;
using MPS.Domain.Exceptions;
using MPS.Infrastructure.Services;
using Xunit;

namespace MPS.Infrastructure.Tests.Services;

public sealed class OrderEvaluationExcelServiceTests
{
    private readonly OrderEvaluationExcelService _sut = new();

    private static OrderEvaluationDto SampleDto(int? score1, int? score2, int? score3) => new(
        Id: 55,
        ProjectId: 7,
        ProjectTitle: "Đơn thử",
        EvaluatorUserId: 3,
        EvaluatorName: "Tổ trưởng A",
        PeriodYear: 2026,
        PeriodMonth: 7,
        Status: "Draft",
        SubmittedAt: null,
        CreatedAt: DateTime.UtcNow,
        RowVersion: "AAAAAAAA",
        Subjects: new[]
        {
            new OrderEvaluationSubjectDto(
                Id: 100, EmployeeUserId: 10, EmployeeName: "Công nhân X", SubjectRoleIdSnapshot: 7,
                FinalScore: null, Note: null,
                Scores: new[]
                {
                    new EvaluationScoreLineDto(201, 1, "Kết quả công việc", "Hoàn thành khối lượng", 45m, 60m, 5, score1, null),
                    new EvaluationScoreLineDto(202, 2, "Kết quả công việc", "Chất lượng", 45m, 40m, 5, score2, null),
                    new EvaluationScoreLineDto(203, 3, "Ý thức kỷ luật", "Chấp hành nội quy", 20m, 100m, 5, score3, null),
                }),
        });

    [Fact]
    public void BuildTemplate_ProducesNonEmptyXlsx()
    {
        var bytes = _sut.BuildTemplate(SampleDto(null, null, null));

        bytes.Should().NotBeNullOrEmpty();
        // .xlsx là ZIP → 2 byte đầu "PK".
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');
    }

    [Fact]
    public void RoundTrip_ParsesBackScoredLines_AndSkipsEmpty()
    {
        // Line 201=4, 202=5, 203=chưa chấm (null) → parse chỉ trả 2 dòng đã điểm.
        var bytes = _sut.BuildTemplate(SampleDto(4, 5, null));

        using var ms = new MemoryStream(bytes);
        var parsed = _sut.ParseScores(ms);

        parsed.Should().HaveCount(1);
        var subject = parsed[0];
        subject.SubjectId.Should().Be(100);
        subject.Lines.Should().HaveCount(2);
        subject.Lines.Should().ContainSingle(l => l.LineId == 201 && l.Score == 4);
        subject.Lines.Should().ContainSingle(l => l.LineId == 202 && l.Score == 5);
        subject.Lines.Should().NotContain(l => l.LineId == 203);
    }

    [Fact]
    public void ParseScores_Throws_WhenNoScores()
    {
        var bytes = _sut.BuildTemplate(SampleDto(null, null, null));
        using var ms = new MemoryStream(bytes);

        var act = () => _sut.ParseScores(ms);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void ParseScores_Throws_OnInvalidFile()
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var act = () => _sut.ParseScores(ms);

        act.Should().Throw<DomainValidationException>();
    }
}
