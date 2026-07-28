using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Excel;
using MPS.Application.Features.Payroll.Common;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;
using PositionEntity = MPS.Domain.Entities.Position;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// GET /payroll/me/payslips/{periodId}/export — NV tải phiếu lương .xlsx.
/// - Trả bytes từ exporter + content-type xlsx + filename slug hoá tên NV
/// - Bỏ dấu tiếng Việt và ký tự lạ khi slug
/// - Tên trống → filename fallback không có slug
/// - Kỳ Draft, không tồn tại, NV không có dòng, user không có EmployeeInformation → 404 gộp
/// </summary>
public sealed class ExportMyPayslipQueryHandlerTests
{
    private const int UserId = 42;
    private const int EmployeeId = 7;
    private const int PeriodId = 20;
    private static readonly byte[] FakeBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // "PK\3\4" — xlsx magic

    private readonly Mock<IPayrollPeriodRepository> _periods = new();
    private readonly Mock<IContractLookupReader> _lookup = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPayrollExcelExporter> _exporter = new();
    private readonly Mock<IPayrollFieldCatalog> _catalog = new();

    private ExportMyPayslipQueryHandler CreateHandler()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _lookup.Setup(l => l.GetEmployeeInformationIdByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmployeeId);
        _exporter.Setup(e => e.Build(It.IsAny<PayrollExcelBook>())).Returns(FakeBytes);
        _catalog.Setup(c => c.GetAll()).Returns(Array.Empty<PayrollFieldDescriptor>());
        return new ExportMyPayslipQueryHandler(
            _periods.Object, _lookup.Object, _currentUser.Object, _exporter.Object, _catalog.Object);
    }

    private static PayrollPeriod BuildPeriod(
        PayrollPeriodStatus status,
        bool includeEmployeeRow = true,
        string? fullName = "Nguyễn Thị Mai")
    {
        // Cột không có FormulaJson để builder không walk AST — tránh phụ thuộc catalog trong test.
        var columns = new List<SalaryColumnDefinition>
        {
            new()
            {
                ColumnCode = PayrollColumnCodes.TongThuNhap, Label = "Tổng thu nhập",
                DisplayOrder = 10, DataType = ColumnDataType.Money,
                ValueSource = ColumnValueSource.Input, IsMoney = true,
            },
            new()
            {
                ColumnCode = PayrollColumnCodes.ThucNhan, Label = "Thực nhận",
                DisplayOrder = 99, DataType = ColumnDataType.Money,
                ValueSource = ColumnValueSource.Input, IsMoney = true,
            },
        };

        var period = new PayrollPeriod
        {
            Id = PeriodId, PeriodYear = 2026, PeriodMonth = 6,
            PayDate = new DateOnly(2026, 6, 30), StandardWorkingDays = 22m,
            Status = status,
            FormulaSnapshotJson = PayrollColumnSnapshotMapper.Serialize(columns),
            PitBracketsSnapshotJson = "[]",
        };

        if (includeEmployeeRow)
        {
            var row = new PayrollRow
            {
                Id = 501, EmployeeInformationId = EmployeeId,
                PayrollPeriodId = PeriodId, PayrollPeriod = period,
                RowType = PayrollRowType.Employee,
                InputSnapshotJson = "{\"Numbers\":{},\"Texts\":{}}",
                EmployeeInformation = fullName is null ? null : new EmployeeInformationEntity
                {
                    Id = EmployeeId,
                    FullName = fullName,
                    Position = new PositionEntity { Id = 3, Name = "Senior Engineer" },
                },
            };
            row.Cells.Add(new PayrollCellValue { ColumnCode = PayrollColumnCodes.TongThuNhap, ComputedValue = 38_000_000m });
            row.Cells.Add(new PayrollCellValue { ColumnCode = PayrollColumnCodes.ThucNhan, ComputedValue = 31_000_000m });
            period.Rows.Add(row);
        }

        return period;
    }

    [Fact]
    public async Task Returns_Xlsx_With_Slugged_Filename_And_Exporter_Bytes()
    {
        _periods.Setup(p => p.GetPeriodWithEmployeeRowAsync(PeriodId, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPeriod(PayrollPeriodStatus.Locked));
        var handler = CreateHandler();

        var file = await handler.Handle(new ExportMyPayslipQuery(PeriodId), CancellationToken.None);

        file.Content.Should().BeSameAs(FakeBytes);
        file.ContentType.Should().Be(ExportPayrollPeriodQueryHandler.XlsxContentType);
        file.FileName.Should().Be("phieu-luong-2026-06-nguyen-thi-mai.xlsx");
    }

    [Theory]
    [InlineData("Đặng Trần Việt Hà", "phieu-luong-2026-06-dang-tran-viet-ha.xlsx")]
    [InlineData("A/B  C_D", "phieu-luong-2026-06-a-b-c-d.xlsx")]
    [InlineData("   ", "phieu-luong-2026-06.xlsx")]
    public async Task Filename_Slugifies_Vietnamese_And_Special_Chars(string fullName, string expected)
    {
        _periods.Setup(p => p.GetPeriodWithEmployeeRowAsync(PeriodId, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPeriod(PayrollPeriodStatus.Locked, fullName: fullName));
        var handler = CreateHandler();

        var file = await handler.Handle(new ExportMyPayslipQuery(PeriodId), CancellationToken.None);

        file.FileName.Should().Be(expected);
    }

    [Fact]
    public async Task Filename_Falls_Back_When_Employee_Missing_On_Row()
    {
        _periods.Setup(p => p.GetPeriodWithEmployeeRowAsync(PeriodId, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPeriod(PayrollPeriodStatus.Locked, fullName: null));
        var handler = CreateHandler();

        var file = await handler.Handle(new ExportMyPayslipQuery(PeriodId), CancellationToken.None);

        file.FileName.Should().Be("phieu-luong-2026-06.xlsx");
    }

    [Fact]
    public async Task Throws_NotFound_When_Period_Missing()
    {
        _periods.Setup(p => p.GetPeriodWithEmployeeRowAsync(PeriodId, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);
        var handler = CreateHandler();

        var act = () => handler.Handle(new ExportMyPayslipQuery(PeriodId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Throws_NotFound_When_Period_Is_Draft()
    {
        _periods.Setup(p => p.GetPeriodWithEmployeeRowAsync(PeriodId, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPeriod(PayrollPeriodStatus.Draft));
        var handler = CreateHandler();

        var act = () => handler.Handle(new ExportMyPayslipQuery(PeriodId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Throws_NotFound_When_Employee_Has_No_Row()
    {
        _periods.Setup(p => p.GetPeriodWithEmployeeRowAsync(PeriodId, EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildPeriod(PayrollPeriodStatus.Locked, includeEmployeeRow: false));
        var handler = CreateHandler();

        var act = () => handler.Handle(new ExportMyPayslipQuery(PeriodId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Throws_NotFound_When_No_Employee_Profile()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _lookup.Setup(l => l.GetEmployeeInformationIdByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        var handler = new ExportMyPayslipQueryHandler(
            _periods.Object, _lookup.Object, _currentUser.Object, _exporter.Object, _catalog.Object);

        var act = () => handler.Handle(new ExportMyPayslipQuery(PeriodId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
