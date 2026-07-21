using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.ContractAppendix.Commands;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Services;

namespace MPS.Application.Tests.Features.ContractAppendix.Integration;

/// <summary>
/// Phase 7 integration tests — full flow: tạo HĐ DRAFT → activate → tạo phụ lục DRAFT
/// (kèm Điều 33 3-day notice) → activate phụ lục → verify HĐ đã apply.
/// </summary>
public sealed class ContractAppendixFlowIntegrationTests
{
    /// <summary>
    /// Happy path: HR tạo phụ lục đổi JobDescription + thêm phụ cấp. Sau khi activate,
    /// HĐ phải có job description mới + allowance mới. Phụ lục Status=Active, ContractFile
    /// row tồn tại với TargetType=Appendix.
    /// </summary>
    [Fact]
    public async Task Full_Happy_Path_Create_Then_Activate_Appendix_Applies_Changes_To_Contract()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var transport = new MPS.Domain.Entities.AllowanceType
        {
            Code = "TRANSPORT",
            Name = "Phụ cấp đi lại",
            DefaultAmount = 500_000m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        await fx.Db.AllowanceTypes.AddAsync(transport);
        await fx.Db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);
        currentUser.Setup(c => c.HasPermission(MPS.Domain.Constants.Permissions.ContractAppendixReadAll)).Returns(true);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var appendixRepo = new ContractAppendixRepository(fx.Db);
        var applier = new AppendixApplier(lookup);
        var storage = new FakeFileStorageService();

        var contractId = await ActivateContractAsync(
            fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var changeSet = new
        {
            jobDescription = "Tech Lead",
            addAllowances = new[]
            {
                new { allowanceTypeId = transport.Id, amount = 700_000m, currency = "VND", note = "đi lại" }
            },
        };
        var changeJson = JsonSerializer.Serialize(changeSet);

        var effectiveFrom = DateTime.UtcNow.AddDays(14);

        var createHandler = new CreateContractAppendixCommandHandler(appendixRepo, repo, currentUser.Object);
        var appendixId = await createHandler.Handle(new CreateContractAppendixCommand(
            ContractId: contractId,
            AppendixNo: "PL-2026-001",
            AppendixType: AppendixType.Amendment,
            Title: "Phụ lục đổi vị trí + phụ cấp đi lại",
            ChangedClausesJson: changeJson,
            AppendixText: "Toàn văn phụ lục...",
            EffectiveFrom: effectiveFrom), CancellationToken.None);

        appendixId.Should().BeGreaterThan(0);

        var draftAppendix = await fx.Db.ContractAppendices.AsNoTracking().FirstAsync(a => a.Id == appendixId);
        draftAppendix.Status.Should().Be(AppendixStatus.Draft);
        draftAppendix.NoticeSentAt.Should().NotBeNull("create handler phải set NoticeSentAt=NOW()");

        var pdfBytes = BuildFakePdfBytes("Phụ lục đã ký");
        var activateHandler = new ActivateContractAppendixCommandHandler(
            appendixRepo, applier, storage, currentUser.Object,
            NullLogger<ActivateContractAppendixCommandHandler>.Instance,
            new NoopPayrollSourceChangeHandler());

        var fileId = await activateHandler.Handle(new ActivateContractAppendixCommand(
            AppendixId: appendixId,
            PdfStream: new MemoryStream(pdfBytes),
            FileSizeBytes: pdfBytes.Length,
            FileName: "PL-2026-001.pdf",
            ContentType: "application/pdf",
            SignedAt: DateTime.UtcNow,
            SignedBySource: "mingo-signing"), CancellationToken.None);

        fileId.Should().BeGreaterThan(0);

        var activated = await fx.Db.ContractAppendices.AsNoTracking().FirstAsync(a => a.Id == appendixId);
        activated.Status.Should().Be(AppendixStatus.Active);
        activated.SignedAt.Should().NotBeNull();

        var updatedContract = await fx.Db.Contracts.AsNoTracking()
            .Include(c => c.ContractAllowances)
            .FirstAsync(c => c.Id == contractId);

        updatedContract.JobDescription.Should().Be("Tech Lead");
        updatedContract.UpdatedAt.Should().NotBeNull();
        updatedContract.ContractAllowances.Should().Contain(a =>
            a.AllowanceTypeId == transport.Id && a.Amount == 700_000m && a.Currency == "VND");

        var file = await fx.Db.ContractFiles.AsNoTracking().FirstAsync(f => f.Id == fileId);
        file.TargetType.Should().Be(FileTargetType.Appendix);
        file.TargetId.Should().Be(appendixId);
        file.Version.Should().Be(1);
        file.IsCurrent.Should().BeTrue();
        file.MinioObjectKey.Should().MatchRegex(@"^appendices/\d{4}/\d{2}/HDLD-[A-Z]+-\d{4}-\d{4}/PL-2026-001-v1\.pdf$");
        storage.Uploaded.Should().ContainKey($"{FakeFileStorageService.DefaultBucket}/{file.MinioObjectKey}");
    }

    /// <summary>
    /// Điều 22 k.2 — phụ lục có key bị cấm (EffectiveTo) → reject ở create handler.
    /// </summary>
    [Fact]
    public async Task Create_Rejects_When_ChangedClausesJson_Contains_Forbidden_Key()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var appendixRepo = new ContractAppendixRepository(fx.Db);
        var storage = new FakeFileStorageService();

        var contractId = await ActivateContractAsync(
            fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var badJson = """{"EffectiveTo": "2030-01-01", "jobDescription": "x"}""";

        var createHandler = new CreateContractAppendixCommandHandler(appendixRepo, repo, currentUser.Object);

        var act = () => createHandler.Handle(new CreateContractAppendixCommand(
            ContractId: contractId,
            AppendixNo: "PL-BAD",
            AppendixType: AppendixType.Amendment,
            Title: "Bad appendix",
            ChangedClausesJson: badJson,
            AppendixText: "...",
            EffectiveFrom: DateTime.UtcNow.AddDays(14)), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*FORBIDDEN_APPENDIX_KEY*EffectiveTo*");

        var count = await fx.Db.ContractAppendices.CountAsync(a => a.ContractId == contractId);
        count.Should().Be(0);
    }

    /// <summary>
    /// Điều 33 k.1 — EffectiveFrom cách NOW < 3 working days → reject ở create handler.
    /// </summary>
    [Fact]
    public async Task Create_Rejects_When_Notice_Less_Than_3_Working_Days()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var appendixRepo = new ContractAppendixRepository(fx.Db);
        var storage = new FakeFileStorageService();

        var contractId = await ActivateContractAsync(
            fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var changeJson = """{"jobDescription": "Senior"}""";

        var createHandler = new CreateContractAppendixCommandHandler(appendixRepo, repo, currentUser.Object);

        var act = () => createHandler.Handle(new CreateContractAppendixCommand(
            ContractId: contractId,
            AppendixNo: "PL-FAST",
            AppendixType: AppendixType.Amendment,
            Title: "Quá gấp",
            ChangedClausesJson: changeJson,
            AppendixText: "...",
            EffectiveFrom: DateTime.UtcNow.AddDays(1)), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*APPENDIX_NOTICE_TOO_SHORT*");

        var count = await fx.Db.ContractAppendices.CountAsync(a => a.ContractId == contractId);
        count.Should().Be(0);
    }

    /// <summary>
    /// Activate trên HĐ đã archived → reject ở repository layer.
    /// </summary>
    [Fact]
    public async Task Activate_Rejects_When_Parent_Contract_Archived()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var appendixRepo = new ContractAppendixRepository(fx.Db);
        var applier = new AppendixApplier(lookup);
        var storage = new FakeFileStorageService();

        var contractId = await ActivateContractAsync(
            fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var changeJson = """{"jobDescription": "Senior"}""";
        var createHandler = new CreateContractAppendixCommandHandler(appendixRepo, repo, currentUser.Object);
        var appendixId = await createHandler.Handle(new CreateContractAppendixCommand(
            ContractId: contractId,
            AppendixNo: "PL-ARCH",
            AppendixType: AppendixType.Amendment,
            Title: "x",
            ChangedClausesJson: changeJson,
            AppendixText: "...",
            EffectiveFrom: DateTime.UtcNow.AddDays(14)), CancellationToken.None);

        var contract = await fx.Db.Contracts.FirstAsync(c => c.Id == contractId);
        contract.ArchivedAt = DateTime.UtcNow;
        await fx.Db.SaveChangesAsync();

        var activateHandler = new ActivateContractAppendixCommandHandler(
            appendixRepo, applier, storage, currentUser.Object,
            NullLogger<ActivateContractAppendixCommandHandler>.Instance,
            new NoopPayrollSourceChangeHandler());

        var pdfBytes = BuildFakePdfBytes();
        var act = () => activateHandler.Handle(new ActivateContractAppendixCommand(
            AppendixId: appendixId,
            PdfStream: new MemoryStream(pdfBytes),
            FileSizeBytes: pdfBytes.Length,
            FileName: "x.pdf",
            ContentType: "application/pdf",
            SignedAt: DateTime.UtcNow,
            SignedBySource: null), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*CONTRACT_ARCHIVED*");
    }

    private static async Task<int> ActivateContractAsync(
        ContractIntegrationFixture fx,
        ContractRepository repo,
        ContractLookupReader lookup,
        ContractPreflightChecker preflight,
        FakeFileStorageService storage,
        ICurrentUserService currentUser,
        int actorId, int employeeInformationId, int partyId, int positionId, int gradeId)
    {
        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser, new ContractNoGenerator(fx.Db));
        var contractId = await createHandler.Handle(
            ContractCommandFactory.ValidCreate(
                employeeInformationId: employeeInformationId,
                employerPartyId: partyId,
                employerSignerUserId: actorId,
                positionId: positionId,
                salaryGradeId: gradeId),
            CancellationToken.None);

        var activateHandler = new ActivateContractCommandHandler(
            repo, lookup, storage, currentUser,
            Mock.Of<IEmployeeInformationRepository>(),
            Mock.Of<IEmployeeAccountProvisioner>(),
            Mock.Of<IEmailSender>(),
            NullLogger<ActivateContractCommandHandler>.Instance);
        var pdf = BuildFakePdfBytes();
        await activateHandler.Handle(new ActivateContractCommand(
            ContractId: contractId,
            PdfStream: new MemoryStream(pdf),
            FileSizeBytes: pdf.Length,
            FileName: "v1.pdf",
            ContentType: "application/pdf",
            SignedAt: DateTime.UtcNow,
            SignedPlace: "HN",
            SignedBySource: null), CancellationToken.None);
        return contractId;
    }

    private static byte[] BuildFakePdfBytes(string body = "fake-pdf-body")
    {
        var header = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
        var payload = System.Text.Encoding.UTF8.GetBytes(body);
        var result = new byte[header.Length + payload.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(payload, 0, result, header.Length, payload.Length);
        return result;
    }
}
