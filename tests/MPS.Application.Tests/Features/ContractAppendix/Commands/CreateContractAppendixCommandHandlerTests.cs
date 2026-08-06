using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.ContractAppendix.Commands;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Interfaces;
using AppendixEntity = MPS.Domain.Entities.ContractAppendix;
using ContractEntity = MPS.Domain.Entities.Contract;

namespace MPS.Application.Tests.Features.ContractAppendix.Commands;

/// <summary>
/// Unit test cho luồng BE tự sinh <c>AppendixNo</c> + suy <c>AppendixType</c> từ ChangedClausesJson.
/// FE giờ không truyền 2 field này, handler phải trả ra đúng giá trị.
/// </summary>
public sealed class CreateContractAppendixCommandHandlerTests
{
    private const int ContractId = 42;
    private const string ContractNo = "HDLD-HDLDN-2026-0007";
    private const int ActorUserId = 5;

    private readonly Mock<IContractAppendixRepository> _appendices = new();
    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public CreateContractAppendixCommandHandlerTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(ActorUserId);
        _contracts.Setup(r => r.GetByIdAsync(ContractId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContractEntity
            {
                Id = ContractId,
                ContractNo = ContractNo,
                Status = ContractStatus.Active,
                PositionId = 1,
                SalaryGradeId = 10,
            });
        _appendices.Setup(r => r.AppendixNoExistsForContractAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _appendices.Setup(r => r.AddDraftAsync(It.IsAny<AppendixEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(999);
    }

    private CreateContractAppendixCommandHandler Sut() =>
        new(_appendices.Object, _contracts.Object, _currentUser.Object);

    /// <summary>ChangedClausesJson chỉ có JobDescription → AppendixType = Detail.</summary>
    [Fact]
    public async Task Derives_Detail_When_Only_JobDescription_Changes()
    {
        _appendices.Setup(r => r.CountForContractAsync(ContractId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await Sut().Handle(new CreateContractAppendixCommand(
            ContractId: ContractId,
            Title: "Cập nhật mô tả",
            ChangedClausesJson: """{"jobDescription": "Tech Lead"}""",
            AppendixText: "Toàn văn...",
            EffectiveFrom: DateTime.UtcNow.AddDays(14)), CancellationToken.None);

        result.AppendixType.Should().Be(AppendixType.Detail.ToString());
        result.AppendixNo.Should().Be($"{ContractNo}-PL-01");
    }

    /// <summary>ChangedClausesJson đổi SalaryGradeId → AppendixType = Amendment.</summary>
    [Fact]
    public async Task Derives_Amendment_When_SalaryGrade_Changes()
    {
        _appendices.Setup(r => r.CountForContractAsync(ContractId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await Sut().Handle(new CreateContractAppendixCommand(
            ContractId: ContractId,
            Title: "Tăng lương",
            ChangedClausesJson: """{"salaryGradeId": 15}""",
            AppendixText: "Toàn văn...",
            EffectiveFrom: DateTime.UtcNow.AddDays(14)), CancellationToken.None);

        result.AppendixType.Should().Be(AppendixType.Amendment.ToString());
        result.AppendixNo.Should().Be($"{ContractNo}-PL-03",
            "seq tiếp theo = count + 1, và pad D2");
    }

    /// <summary>Đổi PositionId cũng phải suy là Amendment (không chỉ salary/allowance).</summary>
    [Fact]
    public async Task Derives_Amendment_When_Position_Changes()
    {
        _appendices.Setup(r => r.CountForContractAsync(ContractId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await Sut().Handle(new CreateContractAppendixCommand(
            ContractId: ContractId,
            Title: "Đổi chức vụ",
            ChangedClausesJson: """{"positionId": 12}""",
            AppendixText: "Toàn văn...",
            EffectiveFrom: DateTime.UtcNow.AddDays(14)), CancellationToken.None);

        result.AppendixType.Should().Be(AppendixType.Amendment.ToString());
    }

    /// <summary>AppendixNo đầu tiên bị trùng (race) → thử số kế tiếp.</summary>
    [Fact]
    public async Task Generates_Next_Available_AppendixNo_When_Candidate_Collides()
    {
        _appendices.Setup(r => r.CountForContractAsync(ContractId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _appendices.Setup(r => r.AppendixNoExistsForContractAsync(
                ContractId, $"{ContractNo}-PL-01", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _appendices.Setup(r => r.AppendixNoExistsForContractAsync(
                ContractId, $"{ContractNo}-PL-02", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Sut().Handle(new CreateContractAppendixCommand(
            ContractId: ContractId,
            Title: "x",
            ChangedClausesJson: """{"jobDescription": "y"}""",
            AppendixText: "z",
            EffectiveFrom: DateTime.UtcNow.AddDays(14)), CancellationToken.None);

        result.AppendixNo.Should().Be($"{ContractNo}-PL-02");
    }

    /// <summary>Handler lưu AppendixNo đã sinh + AppendixType đã suy lên entity trước khi persist.</summary>
    [Fact]
    public async Task Persists_Generated_Fields_On_Draft_Entity()
    {
        _appendices.Setup(r => r.CountForContractAsync(ContractId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        AppendixEntity? captured = null;
        _appendices.Setup(r => r.AddDraftAsync(It.IsAny<AppendixEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AppendixEntity, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(101);

        await Sut().Handle(new CreateContractAppendixCommand(
            ContractId: ContractId,
            Title: "x",
            ChangedClausesJson: """{"addAllowances":[{"allowanceTypeId":9,"amount":100000}]}""",
            AppendixText: "z",
            EffectiveFrom: DateTime.UtcNow.AddDays(14)), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.AppendixNo.Should().Be($"{ContractNo}-PL-01");
        captured.AppendixType.Should().Be(AppendixType.Amendment);
        captured.Status.Should().Be(AppendixStatus.Draft);
        captured.NoticeSentAt.Should().NotBeNull();
        captured.CreatedByUserId.Should().Be(ActorUserId);
    }
}
