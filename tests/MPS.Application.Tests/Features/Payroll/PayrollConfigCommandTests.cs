using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Commands;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

public sealed class PayrollConfigCommandTests
{
    private static Mock<ICurrentUserService> User(int id = 1)
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(u => u.UserId).Returns(id);
        return m;
    }

    // ===== Xoá bảng: chặn bảng mặc định =====
    [Fact]
    public async Task DeleteFormulaSet_WhenDefault_Throws()
    {
        var repo = new Mock<IPayrollFormulaSetRepository>();
        repo.Setup(r => r.GetByIdAsync(5, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollFormulaSet { Id = 5, Name = "Mặc định", IsDefault = true });
        var handler = new DeleteFormulaSetCommandHandler(repo.Object);

        var act = () => handler.Handle(new DeleteFormulaSetCommand(5), default);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*mặc định*");
        repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ===== Xoá bảng: chặn bảng đã dùng cho kỳ =====
    [Fact]
    public async Task DeleteFormulaSet_WhenUsedByPeriod_Throws()
    {
        var repo = new Mock<IPayrollFormulaSetRepository>();
        repo.Setup(r => r.GetByIdAsync(5, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollFormulaSet { Id = 5, Name = "X", IsDefault = false });
        repo.Setup(r => r.IsUsedByAnyPeriodAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handler = new DeleteFormulaSetCommandHandler(repo.Object);

        var act = () => handler.Handle(new DeleteFormulaSetCommand(5), default);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*kỳ lương*");
        repo.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ===== Xoá cột hệ thống bị chặn =====
    [Fact]
    public async Task DeleteColumn_WhenSystem_Throws()
    {
        var repo = new Mock<IPayrollFormulaSetRepository>();
        repo.Setup(r => r.GetColumnAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SalaryColumnDefinition { Id = 10, ColumnCode = "THUCNHAN", IsSystem = true });
        var handler = new DeleteColumnCommandHandler(repo.Object, new ColumnDependencyGraph());

        var act = () => handler.Handle(new DeleteColumnCommand(10), default);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*hệ thống*");
        repo.Verify(r => r.DeleteColumnAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ===== Xoá cột đang bị cột khác tham chiếu bị chặn =====
    [Fact]
    public async Task DeleteColumn_WhenReferencedByAnother_Throws()
    {
        var repo = new Mock<IPayrollFormulaSetRepository>();
        var target = new SalaryColumnDefinition { Id = 10, FormulaSetId = 1, ColumnCode = "MUCLUONG", IsSystem = false };
        var dependant = new SalaryColumnDefinition
        {
            Id = 11, FormulaSetId = 1, ColumnCode = "DOUBLE", ValueSource = ColumnValueSource.Formula,
            FormulaJson = PayrollJson.Serialize(FormulaNode.Mul(FormulaNode.Col("MUCLUONG"), FormulaNode.Const(2))),
        };
        repo.Setup(r => r.GetColumnAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        repo.Setup(r => r.GetColumnsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SalaryColumnDefinition> { target, dependant });
        var handler = new DeleteColumnCommandHandler(repo.Object, new ColumnDependencyGraph());

        var act = () => handler.Handle(new DeleteColumnCommand(10), default);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("DOUBLE");
        repo.Verify(r => r.DeleteColumnAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ===== Clone: deep-copy cột, bỏ cờ hệ thống + mặc định =====
    [Fact]
    public async Task CloneFormulaSet_DeepCopiesColumns_AsUserOwned()
    {
        var source = new PayrollFormulaSet
        {
            Id = 1, Name = "Mặc định", IsDefault = true,
            Columns = new List<SalaryColumnDefinition>
            {
                new() { ColumnCode = "MUCLUONG", Label = "Mức lương", ValueSource = ColumnValueSource.Input, IsSystem = true },
                new() { ColumnCode = "THUCNHAN", Label = "Thực nhận", ValueSource = ColumnValueSource.Input, IsSystem = true },
            },
        };
        var repo = new Mock<IPayrollFormulaSetRepository>();
        repo.Setup(r => r.GetByIdAsync(1, true, It.IsAny<CancellationToken>())).ReturnsAsync(source);
        PayrollFormulaSet? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<PayrollFormulaSet>(), It.IsAny<CancellationToken>()))
            .Callback<PayrollFormulaSet, CancellationToken>((s, _) => captured = s)
            .ReturnsAsync(99);
        var parameters = new Mock<IPayrollParameterRepository>();
        parameters.Setup(p => p.GetAllAsync(It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollParameter>());
        var validator = new Mock<IPayrollFormulaValidator>();
        validator.Setup(v => v.Validate(It.IsAny<IReadOnlyList<SalaryColumnDefinition>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(Array.Empty<string>());

        var handler = new CloneFormulaSetCommandHandler(repo.Object, parameters.Object, validator.Object, User().Object);

        var newId = await handler.Handle(new CloneFormulaSetCommand(1, "Bản của kế toán", null), default);

        newId.Should().Be(99);
        captured.Should().NotBeNull();
        captured!.IsDefault.Should().BeFalse();
        captured.IsActive.Should().BeTrue();
        captured.Columns.Should().HaveCount(2);
        captured.Columns.Should().OnlyContain(c => c.IsSystem == false);
        captured.Columns.Select(c => c.ColumnCode).Should().BeEquivalentTo("MUCLUONG", "THUCNHAN");
    }
}
