using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Repositories;

namespace MPS.Application.Tests.Features.ContractTermination;

/// <summary>
/// Phase 4 — chấm dứt HĐ phải ĐÓNG khoảng hiệu lực (`Contract.EffectiveTo = TerminationDate`),
/// không chỉ đổi `Status`.
///
/// <para><b>Lỗi gốc.</b> `AddAndMarkContractTerminatedAsync` chỉ set `Status`. Với HĐ không xác định
/// thời hạn (`EffectiveTo = null`) thì sau khi NLĐ nghỉ, hợp đồng vẫn "phủ" mọi ngày về sau:</para>
/// <list type="number">
///   <item>chấm công lỡ nhập sau ngày nghỉ vẫn được tính công ⇒ trả tiền cho người đã nghỉ;</item>
///   <item>tháng kế tiếp người đã nghỉ vẫn lọt vào cổng readiness với lỗi "chưa có chấm công"
///         ⇒ <b>chặn tạo kỳ lương của cả công ty</b>.</item>
/// </list>
/// </summary>
public sealed class TerminationClosesEffectiveToTests
{
    private static readonly DateTime TerminationDate = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "HĐ vô thời hạn chấm dứt → EffectiveTo đóng đúng ngày làm việc cuối")]
    public async Task IndefiniteContract_ClosesEffectiveTo()
    {
        using var fx = new ContractIntegrationFixture();
        var contractId = await SeedActiveContractAsync(fx, effectiveTo: null);

        await TerminateAsync(fx, contractId, ContractStatus.Terminated);

        var contract = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contractId);
        contract.EffectiveTo.Should().Be(TerminationDate);
        contract.Status.Should().Be(ContractStatus.Terminated);
    }

    [Fact(DisplayName = "HĐ có hạn chấm dứt sớm → EffectiveTo rút ngắn về ngày chấm dứt")]
    public async Task DefiniteContract_ShortensEffectiveTo()
    {
        using var fx = new ContractIntegrationFixture();
        var contractId = await SeedActiveContractAsync(
            fx, effectiveTo: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        await TerminateAsync(fx, contractId, ContractStatus.Terminated);

        var contract = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contractId);
        contract.EffectiveTo.Should().Be(TerminationDate);
    }

    [Fact(DisplayName = "HĐ đã hết hạn TRƯỚC ngày chấm dứt → KHÔNG kéo dài EffectiveTo")]
    public async Task ContractEndingEarlier_KeepsItsOwnEffectiveTo()
    {
        // Chấm dứt không bao giờ được làm HĐ dài ra: ngày công sau EffectiveTo cũ vẫn phải nằm ngoài.
        var originalEnd = new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc);

        using var fx = new ContractIntegrationFixture();
        var contractId = await SeedActiveContractAsync(fx, effectiveTo: originalEnd);

        await TerminateAsync(fx, contractId, ContractStatus.Terminated);

        var contract = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contractId);
        contract.EffectiveTo.Should().Be(originalEnd);
    }

    [Fact(DisplayName = "Chấm dứt có hiệu lực tương lai (PendingTermination) cũng đóng EffectiveTo ngay")]
    public async Task PendingTermination_ClosesEffectiveToUpfront()
    {
        // Quyết định chấm dứt đã ký thì khoảng hiệu lực đã biết trước — đóng luôn để kỳ lương của
        // tháng có ngày nghỉ tính đúng công, không phải chờ job flip sang Terminated.
        using var fx = new ContractIntegrationFixture();
        var contractId = await SeedActiveContractAsync(fx, effectiveTo: null);

        await TerminateAsync(fx, contractId, ContractStatus.PendingTermination);

        var contract = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contractId);
        contract.EffectiveTo.Should().Be(TerminationDate);
        contract.Status.Should().Be(ContractStatus.PendingTermination);
    }

    private static async Task TerminateAsync(
        ContractIntegrationFixture fx, int contractId, ContractStatus targetStatus)
    {
        var repo = new ContractTerminationRepository(fx.Db);
        await repo.AddAndMarkContractTerminatedAsync(
            new Domain.Entities.ContractTermination
            {
                ContractId = contractId,
                TerminationDate = TerminationDate,
                ReasonRefId = 9,
                TerminationParty = TerminationParty.Employee,
                LegalBasis = "Điều 35 k.1",
                NoticeSentAt = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc),
                NoticeDaysAhead = 51,
                NoNoticeRequired = false,
                DecidedByUserId = 1,
                CreatedAt = DateTime.UtcNow,
            },
            targetStatus,
            CancellationToken.None);
    }

    private static async Task<int> SeedActiveContractAsync(
        ContractIntegrationFixture fx, DateTime? effectiveTo)
    {
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var contract = new Domain.Entities.Contract
        {
            ContractNo = "HDLD-TEST-2026-0001",
            EmployeeInformationId = employeeInfo.Id,
            EmployerPartyId = party.Id,
            EmployerSignerUserId = actor.Id,
            PositionId = position.Id,
            SalaryGradeId = grade.Id,
            SalaryAmountSnapshot = 10_000_000m,
            SalaryCurrencySnapshot = "VND",
            Kind = ContractKind.LaborFullTime,
            ContractCategory = effectiveTo is null ? ContractCategory.Indefinite : ContractCategory.Definite,
            ContractSubtype = ContractSubtype.Official,
            Status = ContractStatus.Active,
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = effectiveTo,
            CreatedAt = DateTime.UtcNow,
        };

        fx.Db.Contracts.Add(contract);
        await fx.Db.SaveChangesAsync();
        return contract.Id;
    }
}
