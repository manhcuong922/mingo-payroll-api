using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.Contract.Queries;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Repositories;

namespace MPS.Application.Tests.Features.Contract.Integration;

/// <summary>
/// Handler-level integration test: exercises Create + AddAllowance + GetById against
/// a real (in-memory) EF Core DbContext + real ContractRepository + real ContractLookupReader
/// + real AutoMapper. Only ICurrentUserService is mocked.
/// Khác với unit test (mock repo): test này xác nhận chuỗi handler → preflight → repo → EF → mapper
/// hoạt động đúng end-to-end ở tầng Application/Infrastructure (không qua HTTP).
/// </summary>
public sealed class ContractFlowIntegrationTests
{
    /// <summary>
    /// Happy-path đầy đủ: HR (actor) tạo HĐ DRAFT cho NLĐ, thêm 1 phụ cấp, rồi đọc lại.
    /// Verify:
    /// - HĐ được lưu vào DB với Status="Draft"
    /// - Snapshot lương = SalaryGrade.BaseSalary (25M)
    /// - AllowanceType navigation property load đúng (Code="LUNCH")
    /// - DTO map enum → string ("Draft", "Definite", "Official")
    /// </summary>
    [Fact]
    public async Task Full_Happy_Path_Creates_Draft_Adds_Allowance_And_Reads_Back()
    {
        // Arrange: spin up in-memory DB + seed actor, employee, party, position, grade, allowance.
        using var fx = new ContractIntegrationFixture();
        var (actor, employee, party, position, grade, allowance) = await fx.SeedBaselineAsync();

        // HR có Contract.Read.All → không bị filter self-scope khi GetById.
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);
        currentUser.Setup(c => c.HasPermission(MPS.Domain.Constants.Permissions.ContractReadAll)).Returns(true);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);

        // === Step 1: HR gọi POST /api/contracts ===
        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser.Object);
        var createCmd = ContractCommandFactory.ValidCreate(
            employeeId: employee.Id,
            employerPartyId: party.Id,
            employerSignerUserId: actor.Id,
            positionId: position.Id,
            salaryGradeId: grade.Id);

        var contractId = await createHandler.Handle(createCmd, CancellationToken.None);

        contractId.Should().BeGreaterThan(0);

        // === Step 2: HR gọi POST /api/contracts/{id}/allowances ===
        var addAllowanceHandler = new AddContractAllowanceCommandHandler(repo, lookup);
        var added = await addAllowanceHandler.Handle(
            new AddContractAllowanceCommand(
                ContractId: contractId,
                AllowanceTypeId: allowance.Id,
                Amount: 730_000m,
                Currency: null, // null → handler điền VND từ SalaryCurrencySnapshot
                Note: "ăn trưa hàng tháng"),
            CancellationToken.None);

        added.Should().BeTrue();

        // === Step 3: HR gọi GET /api/contracts/{id} ===
        var getByIdHandler = new GetContractByIdQueryHandler(repo, fx.Mapper, currentUser.Object);
        var detail = await getByIdHandler.Handle(new GetContractByIdQuery(contractId), CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.Id.Should().Be(contractId);
        detail.ContractNo.Should().Be("HDLD-2026-0001");
        detail.Status.Should().Be("Draft");
        detail.ContractCategory.Should().Be("Definite");
        detail.ContractSubtype.Should().Be("Official");
        detail.SalaryAmountSnapshot.Should().Be(25_000_000m);
        detail.SalaryCurrencySnapshot.Should().Be("VND");
        detail.Allowances.Should().HaveCount(1);
        detail.Allowances[0].AllowanceTypeId.Should().Be(allowance.Id);
        detail.Allowances[0].Code.Should().Be("LUNCH");
        detail.Allowances[0].Amount.Should().Be(730_000m);
        detail.Allowances[0].Currency.Should().Be("VND");
    }

    /// <summary>
    /// Test self-scope deviation #3 của auth module: caller chỉ có Contract.Read.Self
    /// → GET /api/contracts (list) phải filter im lặng về HĐ của chính mình, KHÔNG trả 403.
    /// Setup 2 HĐ thuộc 2 NLĐ khác nhau. NLĐ A đăng nhập → chỉ thấy đúng 1 HĐ của mình.
    /// </summary>
    [Fact]
    public async Task List_Endpoint_Filters_To_Self_When_Caller_Only_Has_ReadSelf()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employee, party, position, grade, _) = await fx.SeedBaselineAsync();

        // Seed thêm 1 NLĐ "stranger" — để có 2 HĐ thuộc 2 NLĐ khác nhau.
        var stranger = new MPS.Domain.Entities.User
        {
            EmployeeCode = "EMP200",
            FullName = "Stranger",
            RoleId = 7,
            Username = "stranger",
            PasswordHash = "x",
            Email = "stranger@mingo.local",
            Dob = new DateTime(1992, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNo = "0",
            Status = 1,
        };
        await fx.Db.Users.AddAsync(stranger);
        await fx.Db.SaveChangesAsync();

        // HR (actor) tạo HĐ cho cả 2 NLĐ.
        var creatorUser = new Mock<ICurrentUserService>();
        creatorUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var createHandler = new CreateContractCommandHandler(repo, preflight, creatorUser.Object);

        // HĐ #1: của employee
        await createHandler.Handle(
            ContractCommandFactory.ValidCreate(
                employeeId: employee.Id,
                employerPartyId: party.Id,
                employerSignerUserId: actor.Id,
                positionId: position.Id,
                salaryGradeId: grade.Id) with { ContractNo = "HDLD-2026-EMP" },
            CancellationToken.None);

        // HĐ #2: của stranger
        await createHandler.Handle(
            ContractCommandFactory.ValidCreate(
                employeeId: stranger.Id,
                employerPartyId: party.Id,
                employerSignerUserId: actor.Id,
                positionId: position.Id,
                salaryGradeId: grade.Id) with { ContractNo = "HDLD-2026-STR" },
            CancellationToken.None);

        // Bây giờ giả lập "employee" đăng nhập (chỉ có Contract.Read.Self, KHÔNG có Read.All).
        var selfUser = new Mock<ICurrentUserService>();
        selfUser.SetupGet(c => c.UserId).Returns(employee.Id);
        selfUser.Setup(c => c.HasPermission(MPS.Domain.Constants.Permissions.ContractReadAll)).Returns(false);
        selfUser.Setup(c => c.HasPermission(MPS.Domain.Constants.Permissions.ContractReadSelf)).Returns(true);

        var listHandler = new GetContractsQueryHandler(repo, fx.Mapper, selfUser.Object);

        // Act: gọi list KHÔNG truyền employeeId filter.
        var result = await listHandler.Handle(
            new GetContractsQuery(EmployeeId: null, Status: null, Category: null, Page: 1, Size: 50),
            CancellationToken.None);

        // Assert: chỉ thấy HĐ của mình (HDLD-2026-EMP), KHÔNG thấy HĐ của stranger.
        // Total cũng phải = 1, không phải 2 (verify count cũng đã filter, không leak qua paging metadata).
        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle(c => c.ContractNo == "HDLD-2026-EMP");
        result.Items.Should().NotContain(c => c.ContractNo == "HDLD-2026-STR");
    }
}
