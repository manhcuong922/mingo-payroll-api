using FluentAssertions;
using Moq;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Commands;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// PayrollSheetTable (FE) và PayrollExcelSalarySheet (Excel) gộp header cha theo DẢI CỘT LIÊN TIẾP
/// cùng GroupName. Add/UpdateColumn phải tự chèn cột vào liền nhóm đã tồn tại, không luôn đặt cuối
/// bảng / giữ nguyên vị trí cũ — nếu không header cha sẽ tách/lặp tên.
/// </summary>
public sealed class GroupedColumnOrderingTests
{
    private static IReadOnlyList<string> NoErrors(IReadOnlyList<SalaryColumnDefinition> _) => Array.Empty<string>();

    private static Mock<IPayrollFormulaValidator> ValidatorAlwaysOk()
    {
        var v = new Mock<IPayrollFormulaValidator>();
        v.Setup(x => x.Validate(It.IsAny<IReadOnlyList<SalaryColumnDefinition>>()))
            .Returns((IReadOnlyList<SalaryColumnDefinition> c) => NoErrors(c));
        return v;
    }

    private static SalaryColumnDefinition Col(int id, string code, string? group, int order) => new()
    {
        Id = id, FormulaSetId = 1, ColumnCode = code, Label = code, GroupName = group, DisplayOrder = order,
        ValueSource = ColumnValueSource.Input,
    };

    // ===== AddColumn: cột mới cùng nhóm với cột đã tồn tại phải chèn liền kề, không đặt cuối =====
    [Fact]
    public async Task AddColumn_GroupAlreadyExists_InsertsRightAfterLastGroupMember()
    {
        // A(0),B(1) nhóm "Thu nhập" · C(2) không nhóm — thêm D nhóm "Thu nhập" phải chèn vào order=2,
        // đẩy C từ 2 → 3, KHÔNG được đặt D ở cuối (order=3) vì sẽ tách rời khỏi A,B.
        var columns = new List<SalaryColumnDefinition>
        {
            Col(1, "A", "Thu nhập", 0),
            Col(2, "B", "Thu nhập", 1),
            Col(3, "C", null, 2),
        };
        var repo = new Mock<IPayrollFormulaSetRepository>();
        repo.Setup(r => r.GetByIdAsync(1, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollFormulaSet { Id = 1, Name = "X" });
        repo.Setup(r => r.ColumnCodeExistsAsync(1, "D", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.GetColumnsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(columns);
        SalaryColumnDefinition? added = null;
        repo.Setup(r => r.AddColumnAsync(It.IsAny<SalaryColumnDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<SalaryColumnDefinition, CancellationToken>((c, _) => added = c)
            .ReturnsAsync(4);

        var handler = new AddColumnCommandHandler(repo.Object, ValidatorAlwaysOk().Object);

        await handler.Handle(new AddColumnCommand(
            1, "D", "D", "Thu nhập", null, ColumnDataType.Money, ColumnValueSource.Input,
            null, null, RoundingMode.None, true, true), default);

        added.Should().NotBeNull();
        added!.DisplayOrder.Should().Be(2);
        // C bị đẩy lùi để nhường chỗ, và phải được persist qua repo, không chỉ sửa in-memory.
        var c = columns.Single(x => x.ColumnCode == "C");
        c.DisplayOrder.Should().Be(3);
        repo.Verify(r => r.UpdateColumnAsync(
            It.Is<SalaryColumnDefinition>(x => x.ColumnCode == "C" && x.DisplayOrder == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ===== AddColumn: không chỉ định nhóm hoặc nhóm mới toanh → giữ hành vi cũ (đặt cuối) =====
    [Fact]
    public async Task AddColumn_NoGroupOrNewGroup_AppendsAtEnd()
    {
        var columns = new List<SalaryColumnDefinition>
        {
            Col(1, "A", "Thu nhập", 0),
            Col(2, "B", null, 1),
        };
        var repo = new Mock<IPayrollFormulaSetRepository>();
        repo.Setup(r => r.GetByIdAsync(1, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayrollFormulaSet { Id = 1, Name = "X" });
        repo.Setup(r => r.ColumnCodeExistsAsync(1, "C", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.GetColumnsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(columns);
        SalaryColumnDefinition? added = null;
        repo.Setup(r => r.AddColumnAsync(It.IsAny<SalaryColumnDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<SalaryColumnDefinition, CancellationToken>((c, _) => added = c)
            .ReturnsAsync(3);

        var handler = new AddColumnCommandHandler(repo.Object, ValidatorAlwaysOk().Object);

        await handler.Handle(new AddColumnCommand(
            1, "C", "C", "Nhóm mới", null, ColumnDataType.Money, ColumnValueSource.Input,
            null, null, RoundingMode.None, true, true), default);

        added!.DisplayOrder.Should().Be(2);
        repo.Verify(r => r.UpdateColumnAsync(It.IsAny<SalaryColumnDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ===== UpdateColumn: đổi GroupName sang 1 nhóm đã có cột khác → tự chèn liền nhóm =====
    [Fact]
    public async Task UpdateColumn_ChangesGroupToExistingGroup_RepositionsAdjacent()
    {
        // A(0),B(1) nhóm "G1" · C(2) không nhóm · D(3) nhóm "G2" — sửa D sang nhóm "G1" phải đặt
        // D ngay sau B (order=2), đẩy C từ 2 → 3.
        var a = Col(1, "A", "G1", 0);
        var b = Col(2, "B", "G1", 1);
        var c = Col(3, "C", null, 2);
        var d = Col(4, "D", "G2", 3);
        var repo = new Mock<IPayrollFormulaSetRepository>();
        repo.Setup(r => r.GetColumnAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(d);
        repo.Setup(r => r.GetColumnsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SalaryColumnDefinition> { a, b, c, d });
        SalaryColumnDefinition? edited = null;
        repo.Setup(r => r.UpdateColumnAsync(It.IsAny<SalaryColumnDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<SalaryColumnDefinition, CancellationToken>((col, _) =>
            {
                if (col.ColumnCode == "D") edited = col;
            })
            .ReturnsAsync(true);

        var handler = new UpdateColumnCommandHandler(repo.Object, ValidatorAlwaysOk().Object);

        await handler.Handle(new UpdateColumnCommand(
            4, "D", "G1", d.DisplayOrder, ColumnDataType.Money, ColumnValueSource.Input,
            null, null, RoundingMode.None, true, true), default);

        edited.Should().NotBeNull();
        edited!.DisplayOrder.Should().Be(2);
        c.DisplayOrder.Should().Be(3); // C bị đẩy lùi trong bộ nhớ (persist qua UpdateColumnAsync riêng).
        repo.Verify(r => r.UpdateColumnAsync(
            It.Is<SalaryColumnDefinition>(x => x.ColumnCode == "C" && x.DisplayOrder == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ===== UpdateColumn: nhóm không đổi → giữ nguyên DisplayOrder FE gửi lên (vị trí đã tự sắp) =====
    [Fact]
    public async Task UpdateColumn_GroupUnchanged_KeepsRequestedDisplayOrder()
    {
        var a = Col(1, "A", "G1", 0);
        var b = Col(2, "B", "G1", 1);
        var repo = new Mock<IPayrollFormulaSetRepository>();
        repo.Setup(r => r.GetColumnAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(b);
        repo.Setup(r => r.GetColumnsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SalaryColumnDefinition> { a, b });
        SalaryColumnDefinition? edited = null;
        repo.Setup(r => r.UpdateColumnAsync(It.IsAny<SalaryColumnDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<SalaryColumnDefinition, CancellationToken>((col, _) => edited = col)
            .ReturnsAsync(true);

        var handler = new UpdateColumnCommandHandler(repo.Object, ValidatorAlwaysOk().Object);

        await handler.Handle(new UpdateColumnCommand(
            2, "B đổi nhãn", "G1", 1, ColumnDataType.Money, ColumnValueSource.Input,
            null, null, RoundingMode.None, true, true), default);

        edited!.DisplayOrder.Should().Be(1);
        edited.Label.Should().Be("B đổi nhãn");
        // Không có cột nào khác bị đụng tới vì nhóm không đổi.
        repo.Verify(r => r.UpdateColumnAsync(
            It.Is<SalaryColumnDefinition>(x => x.ColumnCode == "A"), It.IsAny<CancellationToken>()), Times.Never);
    }
}
