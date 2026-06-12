using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Entities;
using MPS.Infrastructure.Repositories;

namespace MPS.Application.Tests.Features.SalaryGrade;

/// <summary>
/// Integration test cho flow xoá SalaryGrade — verify rule trong plan §4.1:
/// "Soft: IsActive=0 nếu có HĐ tham chiếu, hard nếu chưa từng dùng."
/// </summary>
public sealed class SalaryGradeRepositoryDeleteFlowTests
{
    /// <summary>
    /// Grade KHÔNG có Contract tham chiếu → hard delete (row biến mất hẳn).
    /// </summary>
    [Fact]
    public async Task Hard_Deletes_When_No_Contract_References_The_Grade()
    {
        using var fx = new ContractIntegrationFixture();
        var (_, _, _, position, _, _) = await fx.SeedBaselineAsync();

        var standalone = new Domain.Entities.SalaryGrade
        {
            Code = "SE-1",
            Name = "Senior Engineer Bậc 1",
            PositionId = position.Id,
            Level = 1,
            BaseSalary = 10_000_000m,
            Currency = "VND",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        await fx.Db.SalaryGrades.AddAsync(standalone);
        await fx.Db.SaveChangesAsync();

        var repo = new SalaryGradeRepository(fx.Db);
        var ok = await repo.DeleteOrDeactivateAsync(standalone.Id, CancellationToken.None);

        ok.Should().BeTrue();
        (await fx.Db.SalaryGrades.AsNoTracking().AnyAsync(s => s.Id == standalone.Id))
            .Should().BeFalse("không có FK ràng buộc → row phải biến mất");
    }

    /// <summary>
    /// Grade ĐANG được Contract tham chiếu → soft delete (IsActive=false, row vẫn còn).
    /// </summary>
    [Fact]
    public async Task Soft_Deactivates_When_Contract_References_The_Grade()
    {
        using var fx = new ContractIntegrationFixture();
        // SeedBaseline tự gắn 1 SalaryGrade với 0 contract; cần tạo HĐ tham chiếu nó.
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        // Tạo HĐ tham chiếu grade.
        var contract = new MPS.Domain.Entities.Contract
        {
            ContractNo = "HDLD-REF",
            EmployeeInformationId = employeeInfo.Id,
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
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = actor.Id,
        };
        await fx.Db.Contracts.AddAsync(contract);
        await fx.Db.SaveChangesAsync();

        var repo = new SalaryGradeRepository(fx.Db);
        var ok = await repo.DeleteOrDeactivateAsync(grade.Id, CancellationToken.None);

        ok.Should().BeTrue();

        var saved = await fx.Db.SalaryGrades.AsNoTracking().FirstOrDefaultAsync(s => s.Id == grade.Id);
        saved.Should().NotBeNull("HĐ đang dùng grade → không xoá cứng được");
        saved!.IsActive.Should().BeFalse("phải set IsActive=false");
        saved.UpdatedAt.Should().NotBeNull("phải update UpdatedAt");
    }

    /// <summary>Id không tồn tại → trả false, không throw.</summary>
    [Fact]
    public async Task Returns_False_When_Grade_Not_Found()
    {
        using var fx = new ContractIntegrationFixture();
        await fx.SeedBaselineAsync();

        var repo = new SalaryGradeRepository(fx.Db);
        var ok = await repo.DeleteOrDeactivateAsync(99999, CancellationToken.None);

        ok.Should().BeFalse();
    }
}
