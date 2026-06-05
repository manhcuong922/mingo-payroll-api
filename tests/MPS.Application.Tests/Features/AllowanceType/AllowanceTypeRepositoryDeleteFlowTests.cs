using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Entities;
using MPS.Infrastructure.Repositories;

namespace MPS.Application.Tests.Features.AllowanceType;

/// <summary>
/// Integration test cho flow xoá AllowanceType — verify rule plan §4.2:
/// "Soft: IsActive=0 nếu có HĐ tham chiếu, hard nếu chưa từng dùng."
/// </summary>
public sealed class AllowanceTypeRepositoryDeleteFlowTests
{
    /// <summary>Loại phụ cấp chưa từng gắn ContractAllowance → hard delete.</summary>
    [Fact]
    public async Task Hard_Deletes_When_No_ContractAllowance_References()
    {
        using var fx = new ContractIntegrationFixture();
        await fx.SeedBaselineAsync();

        var standalone = new Domain.Entities.AllowanceType
        {
            Code = "TRANSPORT",
            Name = "Phụ cấp đi lại",
            DefaultAmount = 500_000m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        await fx.Db.AllowanceTypes.AddAsync(standalone);
        await fx.Db.SaveChangesAsync();

        var repo = new AllowanceTypeRepository(fx.Db);
        var ok = await repo.DeleteOrDeactivateAsync(standalone.Id, CancellationToken.None);

        ok.Should().BeTrue();
        (await fx.Db.AllowanceTypes.AsNoTracking().AnyAsync(a => a.Id == standalone.Id))
            .Should().BeFalse();
    }

    /// <summary>Loại phụ cấp ĐANG gắn vào HĐ → soft delete (IsActive=false).</summary>
    [Fact]
    public async Task Soft_Deactivates_When_Referenced_By_ContractAllowance()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employee, party, position, grade, allowance) = await fx.SeedBaselineAsync();

        // Gắn 1 ContractAllowance trỏ về allowance type.
        var contract = new MPS.Domain.Entities.Contract
        {
            ContractNo = "HDLD-AL-REF",
            EmployeeId = employee.Id,
            EmployerPartyId = party.Id,
            EmployerSignerUserId = actor.Id,
            PositionId = position.Id,
            JobDescription = "x",
            WorkLocation = "x",
            EffectiveFrom = DateTime.UtcNow,
            SalaryGradeId = grade.Id,
            SalaryAmountSnapshot = grade.BaseSalary,
            SalaryCurrencySnapshot = grade.Currency,
            WorkingTimeJson = "{}",
            ContractText = "x",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = actor.Id,
        };
        await fx.Db.Contracts.AddAsync(contract);
        await fx.Db.SaveChangesAsync();

        await fx.Db.ContractAllowances.AddAsync(new ContractAllowance
        {
            ContractId = contract.Id,
            AllowanceTypeId = allowance.Id,
            Amount = 500_000m,
            Currency = "VND",
            CreatedAt = DateTime.UtcNow,
        });
        await fx.Db.SaveChangesAsync();

        var repo = new AllowanceTypeRepository(fx.Db);
        var ok = await repo.DeleteOrDeactivateAsync(allowance.Id, CancellationToken.None);

        ok.Should().BeTrue();

        var saved = await fx.Db.AllowanceTypes.AsNoTracking().FirstOrDefaultAsync(a => a.Id == allowance.Id);
        saved.Should().NotBeNull();
        saved!.IsActive.Should().BeFalse("HĐ đang dùng → set IsActive=false");
        saved.UpdatedAt.Should().NotBeNull();
    }

    /// <summary>Id không tồn tại → trả false, không throw.</summary>
    [Fact]
    public async Task Returns_False_When_Allowance_Type_Not_Found()
    {
        using var fx = new ContractIntegrationFixture();
        await fx.SeedBaselineAsync();

        var repo = new AllowanceTypeRepository(fx.Db);
        var ok = await repo.DeleteOrDeactivateAsync(99999, CancellationToken.None);

        ok.Should().BeFalse();
    }
}
