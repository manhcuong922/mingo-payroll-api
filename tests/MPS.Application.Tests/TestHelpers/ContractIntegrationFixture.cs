using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MPS.Application.Mappings;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Employee;
using MPS.Infrastructure.Persistence;

namespace MPS.Application.Tests.TestHelpers;

/// <summary>
/// Spins up an in-memory <see cref="AppDbContext"/> + real AutoMapper for handler-level
/// integration tests. Each fixture gets a unique DB name so tests stay isolated.
/// </summary>
internal sealed class ContractIntegrationFixture : IDisposable
{
    public AppDbContext Db { get; }
    public IMapper Mapper { get; }

    public ContractIntegrationFixture()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"contract-test-{Guid.NewGuid():N}")
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();

        var mapperConfig = new MapperConfiguration(
            cfg => cfg.AddProfile<ContractMappingProfile>(),
            NullLoggerFactory.Instance);
        Mapper = mapperConfig.CreateMapper();
    }

    public async Task<(User actor, EmployeeInformation employeeInfo, ContractParty party, Position position, SalaryGrade grade, AllowanceType allowance)>
        SeedBaselineAsync(CancellationToken ct = default)
    {
        var actor = new User
        {
            EmployeeCode = "HR001",
            FullName = "HR Admin",
            RoleId = 2,
            Username = "hr",
            PasswordHash = "x",
            Email = "hr@mingo.local",
            Dob = new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNo = "0",
            Status = 1,
        };
        var employee = new User
        {
            EmployeeCode = "EMP100",
            FullName = "Nguyễn Văn A",
            RoleId = 7,
            Username = "alice",
            PasswordHash = "x",
            Email = "alice@mingo.local",
            Dob = new DateTime(1995, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            PhoneNo = "0",
            Status = 1,
        };
        await Db.Users.AddRangeAsync(new[] { actor, employee }, ct);

        var party = new ContractParty
        {
            LegalName = "Công ty Mingo",
            TaxCode = "0100000000",
            Address = "Hà Nội",
            RepresentativeFullName = "Nguyễn Văn B",
            RepresentativeTitle = "Giám đốc",
            EffectiveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = null,
            CreatedAt = DateTime.UtcNow,
        };
        await Db.ContractParties.AddAsync(party, ct);

        var position = new Position
        {
            Code = "SE",
            Name = "Senior Backend Engineer",
        };
        await Db.Positions.AddAsync(position, ct);

        await Db.SaveChangesAsync(ct);

        var employeeInfo = new EmployeeInformation
        {
            UserId = employee.Id,
            FullName = "Nguyễn Văn A",
            Dob = new DateTime(1995, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            Gender = Gender.Male,
            CitizenId = "012345678901",
            PermanentAddress = "Hà Nội",
            PositionId = position.Id,
            DependentsNo = 0,
        };
        await Db.EmployeeInformations.AddAsync(employeeInfo, ct);

        var grade = new SalaryGrade
        {
            Code = "SE-3",
            Name = "Senior Engineer Bậc 3",
            PositionId = position.Id,
            Level = 3,
            BaseSalary = 25_000_000m,
            Currency = "VND",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        await Db.SalaryGrades.AddAsync(grade, ct);

        var allowance = new AllowanceType
        {
            Code = "LUNCH",
            Name = "Phụ cấp ăn trưa",
            DefaultAmount = 730_000m,
            IsBhxhBase = false,
            IsTaxable = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        await Db.AllowanceTypes.AddAsync(allowance, ct);

        await Db.SaveChangesAsync(ct);

        return (actor, employeeInfo, party, position, grade, allowance);
    }

    public void Dispose()
    {
        Db.Dispose();
    }
}
