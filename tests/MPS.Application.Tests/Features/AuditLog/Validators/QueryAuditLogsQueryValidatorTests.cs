using FluentAssertions;
using MPS.Application.Features.AuditLog.Queries.QueryAuditLogs;
using MPS.Application.Features.AuditLog.Validators;

namespace MPS.Application.Tests.Features.AuditLog.Validators;

public sealed class QueryAuditLogsQueryValidatorTests
{
    private readonly QueryAuditLogsQueryValidator _sut = new();

    private static QueryAuditLogsQuery Valid(
        int page = 1,
        int size = 20,
        DateTime? from = null,
        DateTime? to = null) =>
        new(null, null, null, null, from, to, page, size);

    [Fact]
    public void Passes_For_Valid_Query()
    {
        var result = _sut.Validate(Valid());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Passes_For_Range_Within_366_Days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(366);

        var result = _sut.Validate(Valid(from: from, to: to));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Fails_When_Page_Is_Zero()
    {
        var result = _sut.Validate(Valid(page: 0));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(QueryAuditLogsQuery.Page));
    }

    [Fact]
    public void Fails_When_Size_Exceeds_Max()
    {
        var result = _sut.Validate(Valid(size: 51));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(QueryAuditLogsQuery.Size));
    }

    [Fact]
    public void Fails_When_Size_Is_Zero()
    {
        var result = _sut.Validate(Valid(size: 0));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_When_From_After_To()
    {
        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(-1);

        var result = _sut.Validate(Valid(from: from, to: to));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("less than or equal"));
    }

    [Fact]
    public void Fails_When_Range_Exceeds_366_Days()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(367);

        var result = _sut.Validate(Valid(from: from, to: to));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("366 days"));
    }
}
