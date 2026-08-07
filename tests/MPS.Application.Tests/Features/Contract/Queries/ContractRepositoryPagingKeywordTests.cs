using FluentAssertions;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Enums.Employee;
using MPS.Infrastructure.Repositories;

namespace MPS.Application.Tests.Features.Contract.Queries;

/// <summary>
/// Keyword search trên GET /api/contracts — filter WHERE (ContractNo LIKE OR EmployeeInformation.FullName LIKE)
/// phải áp dụng lên toàn dataset đã filter (total = số match tuyệt đối, không phải subset của 1 page).
/// </summary>
public sealed class ContractRepositoryPagingKeywordTests
{
    [Fact]
    public async Task GetPagedAsync_KeywordNull_ReturnsAllContracts()
    {
        using var fx = new ContractIntegrationFixture();
        var ctx = await SeedTwoEmployeesWithContractsAsync(fx);
        var repo = new ContractRepository(fx.Db);

        var (items, total) = await repo.GetPagedAsync(
            keyword: null,
            employeeInformationId: null,
            status: null,
            category: null,
            kind: null,
            page: 1,
            size: 20,
            CancellationToken.None);

        total.Should().Be(ctx.TotalContracts);
        items.Should().HaveCount(ctx.TotalContracts);
    }

    [Fact]
    public async Task GetPagedAsync_KeywordMatchesEmployeeFullName_AcrossPages()
    {
        using var fx = new ContractIntegrationFixture();
        var ctx = await SeedTwoEmployeesWithContractsAsync(fx);
        var repo = new ContractRepository(fx.Db);

        // "Nam" xuất hiện ở FullName của employee 1 — dataset có ctx.NamContractCount HĐ cho NLĐ này.
        // Page size nhỏ để chắc chắn total phản ánh toàn bộ match, không phải chỉ 1 page.
        var (items, total) = await repo.GetPagedAsync(
            keyword: "Nam",
            employeeInformationId: null,
            status: null,
            category: null,
            kind: null,
            page: 1,
            size: 2,
            CancellationToken.None);

        total.Should().Be(ctx.NamContractCount);
        items.Should().OnlyContain(c => c.EmployeeInformation.FullName.Contains("Nam"));
    }

    [Fact]
    public async Task GetPagedAsync_KeywordMatchesContractNo()
    {
        using var fx = new ContractIntegrationFixture();
        var ctx = await SeedTwoEmployeesWithContractsAsync(fx);
        var repo = new ContractRepository(fx.Db);

        var (items, total) = await repo.GetPagedAsync(
            keyword: "HDLD-VP",
            employeeInformationId: null,
            status: null,
            category: null,
            kind: null,
            page: 1,
            size: 20,
            CancellationToken.None);

        total.Should().Be(1);
        items.Should().ContainSingle().Which.ContractNo.Should().Be("HDLD-VP-002");
    }

    [Fact]
    public async Task GetPagedAsync_KeywordCombinedWithStatus_UsesAndSemantics()
    {
        using var fx = new ContractIntegrationFixture();
        var ctx = await SeedTwoEmployeesWithContractsAsync(fx);
        var repo = new ContractRepository(fx.Db);

        // "HDLD-SX" match 2 HĐ của Nam (1 Active + 1 Draft). Kết hợp status=Active → chỉ còn 1.
        var (items, total) = await repo.GetPagedAsync(
            keyword: "HDLD-SX",
            employeeInformationId: null,
            status: ContractStatus.Active,
            category: null,
            kind: null,
            page: 1,
            size: 20,
            CancellationToken.None);

        total.Should().Be(1);
        items.Should().ContainSingle().Which.Status.Should().Be(ContractStatus.Active);
    }

    [Fact]
    public async Task GetPagedAsync_KeywordWhitespace_IsIgnored()
    {
        using var fx = new ContractIntegrationFixture();
        var ctx = await SeedTwoEmployeesWithContractsAsync(fx);
        var repo = new ContractRepository(fx.Db);

        var (items, total) = await repo.GetPagedAsync(
            keyword: "   ",
            employeeInformationId: null,
            status: null,
            category: null,
            kind: null,
            page: 1,
            size: 20,
            CancellationToken.None);

        total.Should().Be(ctx.TotalContracts);
    }

    [Fact]
    public async Task GetPagedAsync_KeywordNoMatch_ReturnsEmptyWithZeroTotal()
    {
        using var fx = new ContractIntegrationFixture();
        _ = await SeedTwoEmployeesWithContractsAsync(fx);
        var repo = new ContractRepository(fx.Db);

        var (items, total) = await repo.GetPagedAsync(
            keyword: "khongtontai-xyz",
            employeeInformationId: null,
            status: null,
            category: null,
            kind: null,
            page: 1,
            size: 20,
            CancellationToken.None);

        total.Should().Be(0);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_PageBeyondTotal_ReturnsEmptyItems_TotalStillAccurate()
    {
        using var fx = new ContractIntegrationFixture();
        var ctx = await SeedTwoEmployeesWithContractsAsync(fx);
        var repo = new ContractRepository(fx.Db);

        var (items, total) = await repo.GetPagedAsync(
            keyword: "Nam",
            employeeInformationId: null,
            status: null,
            category: null,
            kind: null,
            page: 999,
            size: 20,
            CancellationToken.None);

        total.Should().Be(ctx.NamContractCount);
        items.Should().BeEmpty();
    }

    private sealed record SeedContext(int TotalContracts, int NamContractCount);

    private static async Task<SeedContext> SeedTwoEmployeesWithContractsAsync(ContractIntegrationFixture fx)
    {
        var (actor, employeeInfoNam, party, position, grade, _) = await fx.SeedBaselineAsync();

        // Baseline fixture đặt FullName="Nguyễn Văn A" — override để keyword "Nam" match.
        employeeInfoNam.FullName = "Nguyễn Văn Nam";

        var employeeInfoHoa = new Domain.Entities.EmployeeInformation
        {
            FullName = "Trần Thị Hoa",
            Dob = new DateTime(1990, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            Gender = Gender.Female,
            CitizenId = "099999999999",
            PermanentAddress = "HCM",
            PositionId = position.Id,
            DependentsNo = 0,
        };
        await fx.Db.EmployeeInformations.AddAsync(employeeInfoHoa);
        await fx.Db.SaveChangesAsync();

        // Nam: 2 HĐ với ContractNo "HDLD-SX-*" (1 Active + 1 Draft)
        await fx.Db.Contracts.AddRangeAsync(
            NewContract("HDLD-SX-001", employeeInfoNam.Id, party.Id, actor.Id, position.Id, grade.Id, ContractStatus.Active),
            NewContract("HDLD-SX-002", employeeInfoNam.Id, party.Id, actor.Id, position.Id, grade.Id, ContractStatus.Draft));

        // Hoa: 1 HĐ với ContractNo "HDLD-VP-002" Active
        await fx.Db.Contracts.AddAsync(
            NewContract("HDLD-VP-002", employeeInfoHoa.Id, party.Id, actor.Id, position.Id, grade.Id, ContractStatus.Active));

        await fx.Db.SaveChangesAsync();

        return new SeedContext(TotalContracts: 3, NamContractCount: 2);
    }

    private static Domain.Entities.Contract NewContract(
        string contractNo, int employeeInformationId, int partyId, int signerId,
        int positionId, int gradeId, ContractStatus status)
        => new()
        {
            ContractNo = contractNo,
            Kind = ContractKind.LaborFullTime,
            ContractCategory = ContractCategory.Indefinite,
            ContractSubtype = ContractSubtype.Official,
            Form = ContractForm.Written,
            EmployeeInformationId = employeeInformationId,
            EmployerPartyId = partyId,
            EmployerSignerUserId = signerId,
            PositionId = positionId,
            JobDescription = "x",
            WorkLocation = "HN",
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = null,
            SalaryGradeId = gradeId,
            SalaryAmountSnapshot = 25_000_000m,
            SalaryCurrencySnapshot = LaborLawConstants.Currency.Default,
            SalaryFormType = SalaryFormType.Time,
            SalaryPayCycle = SalaryPayCycle.Monthly,
            WorkingTimeJson = "{}",
            Status = status,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = signerId,
        };
}
