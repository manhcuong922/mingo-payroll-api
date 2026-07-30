using FluentAssertions;
using MPS.Application.Common.Utilities;

namespace MPS.Application.Tests.Common.Services;

/// <summary>
/// Phát hiện #27 (E2E 2026-07-21): username tự sinh khi kích hoạt HĐ lọt ký tự thô từ họ tên
/// (vd "[E2E] To truong phu" → "mru4yime[ttp…"), gây khó đăng nhập và lỗi phía client.
/// </summary>
public sealed class UsernameGeneratorTests
{
    [Fact]
    public void Generate_StripsDiacritics_AndKeepsExpectedShape()
    {
        UsernameGenerator.Generate("Vũ Đình Thắng", "BA260001").Should().Be("thangvdBA260001");
    }

    [Theory]
    [InlineData("[E2E] To truong phu", "TTSX260001", "phuettTTSX260001")]
    [InlineData("Nguyễn Văn A.", "BA260002", "anvBA260002")]
    [InlineData("O'Brien Minh", "BA260003", "minhoBA260003")]
    public void Generate_DropsNonAlphanumericCharacters(string fullName, string employeeCode, string expected)
    {
        UsernameGenerator.Generate(fullName, employeeCode).Should().Be(expected);
    }

    [Fact]
    public void Generate_ProducesOnlyAsciiAlphanumerics()
    {
        var username = UsernameGenerator.Generate("Trần [Thị] Bích-Ngọc!", "QA260009");

        username.Should().MatchRegex("^[a-zA-Z0-9]+$");
    }

    [Fact]
    public void Generate_FallsBackToEmployeeCode_WhenNameHasNoUsableCharacter()
    {
        UsernameGenerator.Generate("[]", "BA260004").Should().Be("BA260004");
    }
}
