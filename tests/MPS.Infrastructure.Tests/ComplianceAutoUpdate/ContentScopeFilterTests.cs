using FluentAssertions;
using MPS.Infrastructure.ComplianceAutoUpdate.Crawl;

namespace MPS.Infrastructure.Tests.ComplianceAutoUpdate;

public sealed class ContentScopeFilterTests
{
    private readonly ContentScopeFilter _sut = new();

    [Fact]
    public void Filter_KeepsOnlyArticlesContainingScopeKeyword()
    {
        // Arrange
        var text =
            "Điều 1. Phạm vi điều chỉnh chung không liên quan.\n" +
            "Điều 2. Mức lương tối thiểu vùng I là 4.960.000 đồng.\n" +
            "Điều 3. Điều khoản thi hành về tổ chức.";

        // Act
        var scoped = _sut.Filter(text);

        // Assert
        scoped.TotalClauses.Should().Be(3);
        scoped.Clauses.Should().ContainSingle();
        scoped.Clauses[0].ArticleNumber.Should().Be(2);
        scoped.HasScopedContent.Should().BeTrue();
    }

    [Fact]
    public void Filter_ReturnsEmpty_WhenNoScopeKeyword()
    {
        var scoped = _sut.Filter("Điều 1. Quy định về con dấu và trụ sở.");

        scoped.HasScopedContent.Should().BeFalse();
        scoped.Clauses.Should().BeEmpty();
    }

    [Fact]
    public void Filter_ReturnsEmpty_ForBlankInput()
    {
        var scoped = _sut.Filter("   ");

        scoped.TotalClauses.Should().Be(0);
        scoped.HasScopedContent.Should().BeFalse();
    }
}
