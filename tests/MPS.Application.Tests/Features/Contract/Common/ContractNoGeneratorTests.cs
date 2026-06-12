using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Services;

namespace MPS.Application.Tests.Features.Contract.Common;

/// <summary>
/// Tests for ContractNoGenerator — verifies format, sequential increments, and year isolation.
/// Uses in-memory DB seeded with existing Contract rows to simulate real state.
/// </summary>
public sealed class ContractNoGeneratorTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"contractno-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task GenerateAsync_Returns_0001_When_No_Prior_Contracts()
    {
        using var db = CreateDb();
        var gen = new ContractNoGenerator(db);

        var result = await gen.GenerateAsync(ContractCategory.Definite, ContractSubtype.Official, 2026, default);

        result.Should().Be("HDLD-CTXD-2026-0001");
    }

    [Theory]
    [InlineData(ContractCategory.Definite, ContractSubtype.Official, "CTXD")]
    [InlineData(ContractCategory.Indefinite, ContractSubtype.Official, "CTKXD")]
    [InlineData(ContractCategory.Definite, ContractSubtype.Probation, "TV")]
    public async Task GenerateAsync_Uses_Correct_TypeCode(
        ContractCategory category, ContractSubtype subtype, string expectedTypeCode)
    {
        using var db = CreateDb();
        var gen = new ContractNoGenerator(db);

        var result = await gen.GenerateAsync(category, subtype, 2026, default);

        result.Should().StartWith($"HDLD-{expectedTypeCode}-2026-");
    }

    [Fact]
    public async Task GenerateAsync_Increments_Seq_Based_On_Existing_Contracts()
    {
        using var db = CreateDb();

        // Seed 3 existing contracts with CTXD-2026 prefix
        SeedContract(db, "HDLD-CTXD-2026-0001");
        SeedContract(db, "HDLD-CTXD-2026-0002");
        SeedContract(db, "HDLD-CTXD-2026-0003");
        await db.SaveChangesAsync();

        var gen = new ContractNoGenerator(db);
        var result = await gen.GenerateAsync(ContractCategory.Definite, ContractSubtype.Official, 2026, default);

        result.Should().Be("HDLD-CTXD-2026-0004");
    }

    [Fact]
    public async Task GenerateAsync_Does_Not_Count_Different_Year()
    {
        using var db = CreateDb();

        // 2025 contracts should not affect 2026 seq
        SeedContract(db, "HDLD-CTXD-2025-0001");
        SeedContract(db, "HDLD-CTXD-2025-0002");
        await db.SaveChangesAsync();

        var gen = new ContractNoGenerator(db);
        var result = await gen.GenerateAsync(ContractCategory.Definite, ContractSubtype.Official, 2026, default);

        result.Should().Be("HDLD-CTXD-2026-0001");
    }

    [Fact]
    public async Task GenerateAsync_Does_Not_Count_Different_TypeCode()
    {
        using var db = CreateDb();

        // TV contracts should not affect CTXD seq
        SeedContract(db, "HDLD-TV-2026-0001");
        SeedContract(db, "HDLD-TV-2026-0002");
        await db.SaveChangesAsync();

        var gen = new ContractNoGenerator(db);
        var result = await gen.GenerateAsync(ContractCategory.Definite, ContractSubtype.Official, 2026, default);

        result.Should().Be("HDLD-CTXD-2026-0001");
    }

    private static void SeedContract(AppDbContext db, string contractNo)
    {
        db.Contracts.Add(new MPS.Domain.Entities.Contract
        {
            ContractNo = contractNo,
            ContractCategory = ContractCategory.Definite,
            ContractSubtype = ContractSubtype.Official,
            Form = ContractForm.Written,
            EmployeeInformationId = 1,
            EmployerPartyId = 1,
            EmployerSignerUserId = 1,
            PositionId = 1,
            JobDescription = "x",
            WorkLocation = "x",
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SalaryGradeId = 1,
            SalaryAmountSnapshot = 20_000_000m,
            SalaryCurrencySnapshot = "VND",
            SalaryFormType = SalaryFormType.Time,
            SalaryPayCycle = SalaryPayCycle.Monthly,
            WorkingTimeJson = "{}",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = 1,
        });
    }
}
