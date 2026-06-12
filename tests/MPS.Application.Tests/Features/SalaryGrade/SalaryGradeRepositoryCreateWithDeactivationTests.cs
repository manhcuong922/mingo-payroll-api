using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Tests.TestHelpers;
using MPS.Infrastructure.Repositories;

namespace MPS.Application.Tests.Features.SalaryGrade;

/// <summary>
/// Integration test for the deactivate-then-insert flow on
/// <see cref="SalaryGradeRepository.CreateWithDeactivationAsync"/>.
/// </summary>
public sealed class SalaryGradeRepositoryCreateWithDeactivationTests
{
    [Fact]
    public async Task Inserts_New_Grade_When_No_Existing_Active_For_Position_Level()
    {
        using var fx = new ContractIntegrationFixture();
        var (_, _, _, position, existingGrade, _) = await fx.SeedBaselineAsync();

        var repo = new SalaryGradeRepository(fx.Db);
        var newGrade = new MPS.Domain.Entities.SalaryGrade
        {
            Code = $"{position.Code}-4",
            Name = "Senior Engineer Bậc 4",
            PositionId = position.Id,
            Level = 4,
            BaseSalary = 30_000_000m,
            Currency = "VND",
        };

        var newId = await repo.CreateWithDeactivationAsync(newGrade, CancellationToken.None);

        newId.Should().BeGreaterThan(0);
        (await fx.Db.SalaryGrades.AsNoTracking().FirstAsync(s => s.Id == existingGrade.Id))
            .IsActive.Should().BeTrue("bậc cũ ở Level khác phải giữ nguyên active");
        (await fx.Db.SalaryGrades.AsNoTracking().FirstAsync(s => s.Id == newId))
            .IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivates_Existing_Active_Grade_When_PositionId_Level_Collides()
    {
        using var fx = new ContractIntegrationFixture();
        var (_, _, _, position, existingGrade, _) = await fx.SeedBaselineAsync();

        var repo = new SalaryGradeRepository(fx.Db);
        var replacement = new MPS.Domain.Entities.SalaryGrade
        {
            Code = $"{position.Code}-{existingGrade.Level}",
            Name = "Senior Engineer Bậc 3 (revised)",
            PositionId = position.Id,
            Level = existingGrade.Level,
            BaseSalary = 27_500_000m,
            Currency = "VND",
        };

        var newId = await repo.CreateWithDeactivationAsync(replacement, CancellationToken.None);

        newId.Should().BeGreaterThan(0);
        newId.Should().NotBe(existingGrade.Id);

        var old = await fx.Db.SalaryGrades.AsNoTracking().FirstAsync(s => s.Id == existingGrade.Id);
        old.IsActive.Should().BeFalse("bậc cũ cùng (Position, Level) phải bị deactivate");
        old.UpdatedAt.Should().NotBeNull();

        var fresh = await fx.Db.SalaryGrades.AsNoTracking().FirstAsync(s => s.Id == newId);
        fresh.IsActive.Should().BeTrue();
        fresh.BaseSalary.Should().Be(27_500_000m);
    }
}
