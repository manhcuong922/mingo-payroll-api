using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Repositories;

namespace MPS.Application.Tests.BackgroundJobs;

/// <summary>
/// Unit-level test cho 3 method job logic trên <see cref="ContractRepository"/>:
/// AutoConvertExpiredDefiniteAsync, ExpireOverdueContractsAsync, ArchiveOldContractsAsync.
///
/// Dùng InMemory DbContext + nowUtc cố định (không phụ thuộc DateTime.UtcNow) để
/// deterministic assert.
/// </summary>
public sealed class ContractRepositoryJobLogicTests
{
    private static readonly DateTime Now = new(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// AutoConvert: HĐ Definite + Active có EffectiveTo hết hạn quá 30 ngày
    /// → set Category=Indefinite, EffectiveTo=null, ConvertedFromDefiniteFlag=true.
    /// </summary>
    [Fact]
    public async Task AutoConvert_Converts_Definite_Active_Past_30_Day_Cutoff()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var contract = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Definite,
            effectiveFrom: Now.AddYears(-2),
            effectiveTo: Now.AddDays(-31));
        await fx.Db.Contracts.AddAsync(contract);
        await fx.Db.SaveChangesAsync();

        var repo = new ContractRepository(fx.Db);
        var converted = await repo.AutoConvertExpiredDefiniteAsync(Now, CancellationToken.None);

        converted.Should().Be(1);

        var saved = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contract.Id);
        saved.ContractCategory.Should().Be(ContractCategory.Indefinite);
        saved.EffectiveTo.Should().BeNull();
        saved.ConvertedFromDefiniteFlag.Should().BeTrue();
        saved.UpdatedAt.Should().Be(Now);
        saved.Status.Should().Be(ContractStatus.Active, "convert giữ Status=Active, không change");
    }

    /// <summary>
    /// AutoConvert: HĐ Definite hết hạn nhưng MỚI 10 ngày (chưa vượt 30) → KHÔNG convert.
    /// </summary>
    [Fact]
    public async Task AutoConvert_Skips_Definite_Active_Within_30_Day_Grace()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var contract = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Definite,
            effectiveFrom: Now.AddYears(-1),
            effectiveTo: Now.AddDays(-10));
        await fx.Db.Contracts.AddAsync(contract);
        await fx.Db.SaveChangesAsync();

        var repo = new ContractRepository(fx.Db);
        var converted = await repo.AutoConvertExpiredDefiniteAsync(Now, CancellationToken.None);

        converted.Should().Be(0);

        var saved = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contract.Id);
        saved.ContractCategory.Should().Be(ContractCategory.Definite);
    }

    /// <summary>
    /// AutoConvert: HĐ Indefinite không bao giờ bị convert (idempotent + safe).
    /// </summary>
    [Fact]
    public async Task AutoConvert_Skips_Indefinite_Contract()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var contract = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Indefinite,
            effectiveFrom: Now.AddYears(-3),
            effectiveTo: null);
        await fx.Db.Contracts.AddAsync(contract);
        await fx.Db.SaveChangesAsync();

        var repo = new ContractRepository(fx.Db);
        var converted = await repo.AutoConvertExpiredDefiniteAsync(Now, CancellationToken.None);

        converted.Should().Be(0);
    }

    /// <summary>
    /// Expire: HĐ Active hết hạn + đã có HĐ con → set Status=Expired.
    /// </summary>
    [Fact]
    public async Task Expire_Sets_Status_Expired_When_Has_Child_Contract()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var parentContract = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Definite,
            effectiveFrom: Now.AddYears(-2),
            effectiveTo: Now.AddDays(-5));
        parentContract.ContractNo = "PARENT-001";
        await fx.Db.Contracts.AddAsync(parentContract);
        await fx.Db.SaveChangesAsync();

        var childContract = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Definite,
            effectiveFrom: Now.AddDays(-4),
            effectiveTo: Now.AddYears(1));
        childContract.ContractNo = "CHILD-001";
        childContract.ParentContractId = parentContract.Id;
        await fx.Db.Contracts.AddAsync(childContract);
        await fx.Db.SaveChangesAsync();

        var repo = new ContractRepository(fx.Db);
        var (expired, affectedEmployeeIds) = await repo.ExpireOverdueContractsAsync(Now, CancellationToken.None);

        expired.Should().Be(1);
        affectedEmployeeIds.Should().ContainSingle().Which.Should().Be(employeeInfo.Id);

        var parentSaved = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == parentContract.Id);
        parentSaved.Status.Should().Be(ContractStatus.Expired);

        var childSaved = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == childContract.Id);
        childSaved.Status.Should().Be(ContractStatus.Active, "HĐ con không bị job đụng tới");
    }

    /// <summary>
    /// Expire: HĐ Active hết hạn nhưng KHÔNG có HĐ con → để AutoConvertJob xử lý sau 30 ngày.
    /// </summary>
    [Fact]
    public async Task Expire_Skips_Contract_Without_Child_Even_If_EffectiveTo_Past()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var contract = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Definite,
            effectiveFrom: Now.AddYears(-1),
            effectiveTo: Now.AddDays(-5));
        await fx.Db.Contracts.AddAsync(contract);
        await fx.Db.SaveChangesAsync();

        var repo = new ContractRepository(fx.Db);
        var (expired, affectedEmployeeIds) = await repo.ExpireOverdueContractsAsync(Now, CancellationToken.None);

        expired.Should().Be(0);
        affectedEmployeeIds.Should().BeEmpty();

        var saved = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contract.Id);
        saved.Status.Should().Be(ContractStatus.Active);
    }

    /// <summary>Archive: Terminated &gt; 10y → set ArchivedAt.</summary>
    [Fact]
    public async Task Archive_Archives_Terminated_Contract_Older_Than_10_Years()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var contract = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Indefinite,
            effectiveFrom: Now.AddYears(-15),
            effectiveTo: null);
        contract.Status = ContractStatus.Terminated;
        await fx.Db.Contracts.AddAsync(contract);
        await fx.Db.SaveChangesAsync();

        var termination = new ContractTermination
        {
            ContractId = contract.Id,
            TerminationDate = Now.AddYears(-11),
            ReasonRefId = 6,
            TerminationParty = TerminationParty.ByLaw,
            LegalBasis = "Điều 34 k.6",
            DecidedByUserId = actor.Id,
            CreatedAt = Now.AddYears(-11),
        };
        await fx.Db.ContractTerminations.AddAsync(termination);
        await fx.Db.SaveChangesAsync();

        var repo = new ContractRepository(fx.Db);
        var archived = await repo.ArchiveOldContractsAsync(Now, CancellationToken.None);

        archived.Should().Be(1);

        var saved = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contract.Id);
        saved.ArchivedAt.Should().Be(Now);
    }

    /// <summary>Archive: Expired &gt; 10y → set ArchivedAt.</summary>
    [Fact]
    public async Task Archive_Archives_Expired_Contract_Older_Than_10_Years()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var contract = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Definite,
            effectiveFrom: Now.AddYears(-12),
            effectiveTo: Now.AddYears(-11));
        contract.Status = ContractStatus.Expired;
        await fx.Db.Contracts.AddAsync(contract);
        await fx.Db.SaveChangesAsync();

        var repo = new ContractRepository(fx.Db);
        var archived = await repo.ArchiveOldContractsAsync(Now, CancellationToken.None);

        archived.Should().Be(1);

        var saved = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contract.Id);
        saved.ArchivedAt.Should().Be(Now);
    }

    /// <summary>Archive: HĐ chưa đủ 10 năm → bỏ qua.</summary>
    [Fact]
    public async Task Archive_Skips_Contract_Younger_Than_10_Years()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var contract = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Definite,
            effectiveFrom: Now.AddYears(-5),
            effectiveTo: Now.AddYears(-3));
        contract.Status = ContractStatus.Expired;
        await fx.Db.Contracts.AddAsync(contract);
        await fx.Db.SaveChangesAsync();

        var repo = new ContractRepository(fx.Db);
        var archived = await repo.ArchiveOldContractsAsync(Now, CancellationToken.None);

        archived.Should().Be(0);
    }

    /// <summary>
    /// Deactivate: NLĐ có HĐ vừa Expire và không còn HĐ Active nào → User.Status chuyển 1 → 0.
    /// </summary>
    [Fact]
    public async Task DeactivateForEmployeesWithoutActiveContract_Sets_User_Inactive_When_No_Active_Contract_Remains()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var expired = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Definite,
            effectiveFrom: Now.AddYears(-1),
            effectiveTo: Now.AddDays(-1));
        expired.Status = ContractStatus.Expired;
        await fx.Db.Contracts.AddAsync(expired);
        await fx.Db.SaveChangesAsync();

        var repo = new UserRepository(fx.Db);
        var deactivated = await repo.DeactivateForEmployeesWithoutActiveContractAsync(
            new[] { employeeInfo.Id }, Now, CancellationToken.None);

        deactivated.Should().Be(1);

        var employeeUser = await fx.Db.Users.AsNoTracking().FirstAsync(u => u.Id == employeeInfo.UserId!.Value);
        employeeUser.Status.Should().Be(0);
    }

    /// <summary>
    /// Deactivate: NLĐ vẫn còn 1 HĐ Active khác → giữ nguyên User.Status = 1 (không khoá nhầm).
    /// </summary>
    [Fact]
    public async Task DeactivateForEmployeesWithoutActiveContract_Keeps_User_Active_When_Another_Active_Contract_Exists()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var expired = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Definite,
            effectiveFrom: Now.AddYears(-1),
            effectiveTo: Now.AddDays(-1));
        expired.Status = ContractStatus.Expired;
        expired.ContractNo = "OLD-001";
        await fx.Db.Contracts.AddAsync(expired);

        var stillActive = NewActiveContract(
            employeeInfo.Id, party.Id, actor.Id, position.Id, grade.Id,
            ContractCategory.Indefinite,
            effectiveFrom: Now.AddDays(-30),
            effectiveTo: null);
        stillActive.ContractNo = "NEW-001";
        await fx.Db.Contracts.AddAsync(stillActive);
        await fx.Db.SaveChangesAsync();

        var repo = new UserRepository(fx.Db);
        var deactivated = await repo.DeactivateForEmployeesWithoutActiveContractAsync(
            new[] { employeeInfo.Id }, Now, CancellationToken.None);

        deactivated.Should().Be(0);

        var employeeUser = await fx.Db.Users.AsNoTracking().FirstAsync(u => u.Id == employeeInfo.UserId!.Value);
        employeeUser.Status.Should().Be(1);
    }

    private static Contract NewActiveContract(
        int employeeInformationId, int partyId, int signerId, int positionId, int gradeId,
        ContractCategory category, DateTime effectiveFrom, DateTime? effectiveTo)
        => new()
        {
            ContractNo = $"HDLD-{Guid.NewGuid():N}".Substring(0, 30),
            ContractCategory = category,
            ContractSubtype = ContractSubtype.Official,
            Form = ContractForm.Written,
            EmployeeInformationId = employeeInformationId,
            EmployerPartyId = partyId,
            EmployerSignerUserId = signerId,
            PositionId = positionId,
            JobDescription = "x",
            WorkLocation = "HN",
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            SalaryGradeId = gradeId,
            SalaryAmountSnapshot = 25_000_000m,
            SalaryCurrencySnapshot = LaborLawConstants.Currency.Default,
            SalaryFormType = SalaryFormType.Time,
            SalaryPayCycle = SalaryPayCycle.Monthly,
            WorkingTimeJson = "{}",
            Status = ContractStatus.Active,
            CreatedAt = effectiveFrom,
            CreatedByUserId = signerId,
        };
}
