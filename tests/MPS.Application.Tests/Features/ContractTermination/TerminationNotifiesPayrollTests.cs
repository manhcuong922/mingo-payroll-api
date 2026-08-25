using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.ContractTermination.Commands;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Repositories;

namespace MPS.Application.Tests.Features.ContractTermination;

/// <summary>
/// Phase 4 — chấm dứt HĐ là một thay đổi NGUỒN của bảng lương (ngày công của NLĐ bị cắt từ
/// TerminationDate) nên phải báo cho <see cref="IPayrollSourceChangeHandler"/>.
///
/// <para><b>Lỗi gốc.</b> Chỉ kích hoạt HĐ và ký phụ lục gọi cổng này; chấm dứt HĐ thì không. Kỳ
/// lương Draft đã tạo trước đó không được đánh cờ stale ⇒ kế toán chốt lương trên số liệu tính khi
/// NLĐ còn đi làm cả tháng.</para>
///
/// <para>Best-effort như mọi side-effect khác: lỗi ở nhánh này KHÔNG được làm hỏng việc chấm dứt HĐ
/// (bản ghi đã commit, ném lỗi ra ngoài chỉ tạo trạng thái nửa vời).</para>
/// </summary>
public sealed class TerminationNotifiesPayrollTests
{
    private static readonly DateTime TerminationDate = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NoticeSentAt = new(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "Chấm dứt HĐ → báo payroll đúng tháng của ngày làm việc cuối")]
    public async Task Termination_NotifiesPayrollForTerminationMonth()
    {
        using var fx = new ContractIntegrationFixture();
        var (contractId, employeeInformationId) = await SeedActiveContractAsync(fx);

        var payroll = new Mock<IPayrollSourceChangeHandler>();

        await TerminateAsync(fx, contractId, payroll.Object);

        payroll.Verify(p => p.OnSourceChangedAsync(
                TerminationDate.Year,
                TerminationDate.Month,
                employeeInformationId,
                It.Is<string>(reason => reason.Contains("Chấm dứt")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Cổng payroll lỗi → việc chấm dứt HĐ vẫn thành công")]
    public async Task PayrollNotificationFailure_DoesNotBreakTermination()
    {
        using var fx = new ContractIntegrationFixture();
        var (contractId, _) = await SeedActiveContractAsync(fx);

        var payroll = new Mock<IPayrollSourceChangeHandler>();
        payroll
            .Setup(p => p.OnSourceChangedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("payroll down"));

        var act = () => TerminateAsync(fx, contractId, payroll.Object);

        await act.Should().NotThrowAsync();
        fx.Db.ContractTerminations.Count(t => t.ContractId == contractId).Should().Be(1);
    }

    private static async Task TerminateAsync(
        ContractIntegrationFixture fx, int contractId, IPayrollSourceChangeHandler payroll)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(1);

        var handler = new CreateContractTerminationCommandHandler(
            new ContractTerminationRepository(fx.Db),
            new ContractRepository(fx.Db),
            new ContractLookupReader(fx.Db),
            currentUser.Object,
            new EmployeeInformationRepository(fx.Db),
            new ProjectTaskRepository(fx.Db),
            new FakeClock(new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc)),
            payroll,
            NullLogger<CreateContractTerminationCommandHandler>.Instance);

        await handler.Handle(new CreateContractTerminationCommand(
            ContractId: contractId,
            ReasonRefId: 6,                       // C6 — không yêu cầu văn bản báo trước
            TerminationParty: TerminationParty.Employer,
            TerminationDate: TerminationDate,
            NoticeSentAt: NoticeSentAt,
            NoticeDaysAhead: null,
            NoNoticeRequired: true,
            WrittenNoticeFileId: null,
            LegalBasis: "Điều 34",
            Notes: null), CancellationToken.None);
    }

    private static async Task<(int ContractId, int EmployeeInformationId)> SeedActiveContractAsync(
        ContractIntegrationFixture fx)
    {
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var contract = new Domain.Entities.Contract
        {
            ContractNo = "HDLD-TEST-2026-0002",
            EmployeeInformationId = employeeInfo.Id,
            EmployerPartyId = party.Id,
            EmployerSignerUserId = actor.Id,
            PositionId = position.Id,
            SalaryGradeId = grade.Id,
            SalaryAmountSnapshot = 10_000_000m,
            SalaryCurrencySnapshot = "VND",
            Kind = ContractKind.LaborFullTime,
            ContractCategory = ContractCategory.Indefinite,
            ContractSubtype = ContractSubtype.Official,
            Status = ContractStatus.Active,
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = null,
            CreatedAt = DateTime.UtcNow,
        };

        fx.Db.Contracts.Add(contract);
        await fx.Db.SaveChangesAsync();
        return (contract.Id, employeeInfo.Id);
    }
}
