using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    /// <summary>
    /// Phase 6 happy path: tạo DRAFT → bump SalaryGrade.BaseSalary lên 30M để chứng minh
    /// activation đọc lại snapshot fresh (không phải snapshot lúc tạo) → upload PDF →
    /// verify Status=Active, snapshot=30M, ContractFile row đầy đủ + SHA-256 khớp blob trong storage.
    /// </summary>
    [Fact]
    public async Task Activate_Freezes_Fresh_SalaryGrade_Snapshot_And_Persists_ContractFile()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employee, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);
        currentUser.Setup(c => c.HasPermission(MPS.Domain.Constants.Permissions.ContractReadAll)).Returns(true);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var storage = new FakeFileStorageService();

        // === Step 1: create draft (snapshot = 25M) ===
        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser.Object);
        var contractId = await createHandler.Handle(
            ContractCommandFactory.ValidCreate(
                employeeId: employee.Id,
                employerPartyId: party.Id,
                employerSignerUserId: actor.Id,
                positionId: position.Id,
                salaryGradeId: grade.Id),
            CancellationToken.None);

        // === Step 2: HR bump SalaryGrade.BaseSalary 25M → 30M (HR điều chỉnh thang lương) ===
        grade.BaseSalary = 30_000_000m;
        await fx.Db.SaveChangesAsync();

        // === Step 3: activate with fresh PDF blob ===
        var pdfBytes = BuildFakePdfBytes("Toàn văn HĐ đã ký");
        var pdfStream = new MemoryStream(pdfBytes);

        var activateHandler = new ActivateContractCommandHandler(
            repo, lookup, storage, currentUser.Object,
            NullLogger<ActivateContractCommandHandler>.Instance);

        var fileId = await activateHandler.Handle(new ActivateContractCommand(
            ContractId: contractId,
            PdfStream: pdfStream,
            FileSizeBytes: pdfBytes.Length,
            FileName: "HDLD-2026-0001-v1.pdf",
            ContentType: "application/pdf",
            SignedAt: DateTime.UtcNow.AddMinutes(-1),
            SignedPlace: "Hà Nội",
            SignedBySource: "mingo-signing"), CancellationToken.None);

        fileId.Should().BeGreaterThan(0);

        // === Verify Contract row ===
        var saved = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contractId);
        saved.Status.Should().Be(ContractStatus.Active);
        saved.SalaryAmountSnapshot.Should().Be(30_000_000m,
            "activation must read SalaryGrade.BaseSalary fresh, not the value at draft creation");
        saved.SalaryCurrencySnapshot.Should().Be("VND");
        saved.SignedPlace.Should().Be("Hà Nội");
        saved.SignedAt.Should().NotBeNull();
        saved.UpdatedAt.Should().NotBeNull();

        // === Verify ContractFile row ===
        var file = await fx.Db.ContractFiles.AsNoTracking().FirstAsync(f => f.Id == fileId);
        file.TargetType.Should().Be(FileTargetType.Contract);
        file.TargetId.Should().Be(contractId);
        file.Version.Should().Be(1);
        file.IsCurrent.Should().BeTrue();
        file.FileName.Should().Be("HDLD-2026-0001-v1.pdf");
        file.FileSizeBytes.Should().Be(pdfBytes.Length);
        file.SignedBySource.Should().Be("mingo-signing");
        file.UploadedByUserId.Should().Be(actor.Id);

        // === Verify SHA-256 of stored blob ===
        var expectedSha = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(pdfBytes)).ToLowerInvariant();
        file.Sha256.Should().Be(expectedSha);

        // === Verify MinIO storage has the blob at the conventional object key ===
        storage.Uploaded.Should().ContainKey($"{FakeFileStorageService.DefaultBucket}/{file.MinioObjectKey}");
        file.MinioObjectKey.Should().MatchRegex(@"^contracts/\d{4}/\d{2}/HDLD-2026-0001/v1\.pdf$");
    }

    /// <summary>
    /// Activation of a non-Draft contract must reject: cố tình hủy HĐ (DRAFT→CANCELLED)
    /// rồi activate → phải throw DomainValidationException.
    /// </summary>
    [Fact]
    public async Task Activate_Rejects_When_Contract_Is_Not_Draft()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employee, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var storage = new FakeFileStorageService();

        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser.Object);
        var contractId = await createHandler.Handle(
            ContractCommandFactory.ValidCreate(
                employeeId: employee.Id, employerPartyId: party.Id,
                employerSignerUserId: actor.Id, positionId: position.Id,
                salaryGradeId: grade.Id),
            CancellationToken.None);

        var cancelHandler = new CancelContractCommandHandler(repo);
        await cancelHandler.Handle(new CancelContractCommand(contractId), CancellationToken.None);

        var activateHandler = new ActivateContractCommandHandler(
            repo, lookup, storage, currentUser.Object,
            NullLogger<ActivateContractCommandHandler>.Instance);

        var pdfBytes = BuildFakePdfBytes();
        var act = () => activateHandler.Handle(new ActivateContractCommand(
            ContractId: contractId,
            PdfStream: new MemoryStream(pdfBytes),
            FileSizeBytes: pdfBytes.Length,
            FileName: "x.pdf",
            ContentType: "application/pdf",
            SignedAt: DateTime.UtcNow,
            SignedPlace: "HN",
            SignedBySource: null), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*only Draft can be activated*");

        // Storage must NOT have the blob — validation should have thrown before upload.
        storage.Uploaded.Should().BeEmpty();
    }

    /// <summary>
    /// Replace flow: activate v1, then replace → v2 becomes IsCurrent, v1 stays IsCurrent=false.
    /// Verifies history preservation per plan §7.4.
    /// </summary>
    [Fact]
    public async Task ReplaceFile_Adds_New_Version_And_Demotes_Old()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employee, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var storage = new FakeFileStorageService();

        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser.Object);
        var contractId = await createHandler.Handle(
            ContractCommandFactory.ValidCreate(
                employeeId: employee.Id, employerPartyId: party.Id,
                employerSignerUserId: actor.Id, positionId: position.Id,
                salaryGradeId: grade.Id),
            CancellationToken.None);

        var activateHandler = new ActivateContractCommandHandler(
            repo, lookup, storage, currentUser.Object,
            NullLogger<ActivateContractCommandHandler>.Instance);

        var v1Bytes = BuildFakePdfBytes("v1 content");
        var v1Id = await activateHandler.Handle(new ActivateContractCommand(
            ContractId: contractId,
            PdfStream: new MemoryStream(v1Bytes),
            FileSizeBytes: v1Bytes.Length,
            FileName: "v1.pdf",
            ContentType: "application/pdf",
            SignedAt: DateTime.UtcNow,
            SignedPlace: "HN",
            SignedBySource: null), CancellationToken.None);

        // Replace with v2
        var replaceHandler = new ReplaceContractFileCommandHandler(
            repo, storage, currentUser.Object,
            NullLogger<ReplaceContractFileCommandHandler>.Instance);

        var v2Bytes = BuildFakePdfBytes("v2 content (đã sửa)");
        var v2Id = await replaceHandler.Handle(new ReplaceContractFileCommand(
            ContractId: contractId,
            PdfStream: new MemoryStream(v2Bytes),
            FileSizeBytes: v2Bytes.Length,
            FileName: "v2.pdf",
            ContentType: "application/pdf",
            SignedBySource: "mingo-signing"), CancellationToken.None);

        v2Id.Should().NotBe(v1Id);

        var files = await fx.Db.ContractFiles.AsNoTracking()
            .Where(f => f.TargetId == contractId)
            .OrderBy(f => f.Version)
            .ToListAsync();

        files.Should().HaveCount(2);
        files[0].Version.Should().Be(1);
        files[0].IsCurrent.Should().BeFalse("v1 must be demoted to keep history");
        files[1].Version.Should().Be(2);
        files[1].IsCurrent.Should().BeTrue();
        files[1].FileName.Should().Be("v2.pdf");
    }

    /// <summary>
    /// Helper: build a byte[] starting with valid %PDF- magic bytes so PdfStreamValidator passes.
    /// Followed by arbitrary content (irrelevant — module này không parse PDF).
    /// </summary>
    private static byte[] BuildFakePdfBytes(string body = "fake-pdf-body")
    {
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-
        var payload = System.Text.Encoding.UTF8.GetBytes(body);
        var result = new byte[header.Length + payload.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(payload, 0, result, header.Length, payload.Length);
        return result;
    }
}
