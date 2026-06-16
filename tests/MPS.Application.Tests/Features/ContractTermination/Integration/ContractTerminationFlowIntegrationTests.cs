using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.ContractTermination.Commands;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Services;

namespace MPS.Application.Tests.Features.ContractTermination.Integration;

/// <summary>
/// Phase 8 integration tests — full flow: tạo HĐ → activate → upload notice → terminate → verify
/// HĐ chuyển Status=Terminated + ContractTermination row đầy đủ.
/// </summary>
public sealed class ContractTerminationFlowIntegrationTests
{
    /// <summary>
    /// Happy path: HR upload notice trước → terminate với WrittenNoticeFileId → verify chuỗi.
    /// Lý do C9 (NLĐ đơn phương) yêu cầu WrittenNotice + báo trước 45 ngày (HĐ vô thời hạn).
    /// </summary>
    [Fact]
    public async Task Full_Happy_Path_Upload_Notice_Then_Terminate_Indefinite_Contract()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var termRepo = new ContractTerminationRepository(fx.Db);
        var storage = new FakeFileStorageService();

        // Tạo HĐ vô thời hạn (Indefinite) — yêu cầu báo trước 45 ngày.
        var createCmd = ContractCommandFactory.ValidCreate(
            category: ContractCategory.Indefinite,
            subtype: ContractSubtype.Official,
            employeeInformationId: employeeInfo.Id,
            employerPartyId: party.Id,
            employerSignerUserId: actor.Id,
            positionId: position.Id,
            salaryGradeId: grade.Id) with
        {
            EffectiveTo = null,
        };

        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser.Object, new ContractNoGenerator(fx.Db));
        var contractId = await createHandler.Handle(createCmd, CancellationToken.None);

        var activateHandler = new ActivateContractCommandHandler(
            repo, lookup, storage, currentUser.Object,
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

        // === Step 1: HR upload notice PDF ===
        var noticeHandler = new UploadTerminationNoticeCommandHandler(
            repo, storage, currentUser.Object,
            NullLogger<UploadTerminationNoticeCommandHandler>.Instance);
        var noticePdf = BuildFakePdfBytes("Quyết định chấm dứt HĐLĐ");
        var fileId = await noticeHandler.Handle(new UploadTerminationNoticeCommand(
            ContractId: contractId,
            PdfStream: new MemoryStream(noticePdf),
            FileSizeBytes: noticePdf.Length,
            FileName: "notice.pdf",
            ContentType: "application/pdf",
            SignedBySource: "mingo-signing"), CancellationToken.None);

        fileId.Should().BeGreaterThan(0);

        var noticeFile = await fx.Db.ContractFiles.AsNoTracking().FirstAsync(f => f.Id == fileId);
        noticeFile.TargetType.Should().Be(FileTargetType.Termination);
        noticeFile.TargetId.Should().Be(contractId);
        noticeFile.IsCurrent.Should().BeTrue();
        noticeFile.MinioObjectKey.Should().MatchRegex(@"^terminations/\d{4}/\d{2}/HDLD-[A-Z]+-\d{4}-\d{4}/notice-v1\.pdf$");

        // === Step 2: terminate với reason C9 ===
        var createTermHandler = new CreateContractTerminationCommandHandler(
            termRepo, repo, lookup, currentUser.Object);

        var noticeSent = DateTime.UtcNow;
        var terminationId = await createTermHandler.Handle(new CreateContractTerminationCommand(
            ContractId: contractId,
            ReasonRefId: 9,
            TerminationParty: TerminationParty.Employee,
            TerminationDate: noticeSent.AddDays(50),
            NoticeSentAt: noticeSent,
            NoticeDaysAhead: 50,
            NoNoticeRequired: false,
            WrittenNoticeFileId: fileId,
            LegalBasis: "Điều 35 k.1",
            Notes: "NLĐ xin nghỉ"), CancellationToken.None);

        terminationId.Should().BeGreaterThan(0);

        var contractAfter = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contractId);
        contractAfter.Status.Should().Be(ContractStatus.Terminated);
        contractAfter.UpdatedAt.Should().NotBeNull();

        var term = await fx.Db.ContractTerminations.AsNoTracking()
            .Include(t => t.ReasonRef)
            .FirstAsync(t => t.Id == terminationId);
        term.ContractId.Should().Be(contractId);
        term.ReasonRef.Code.Should().Be(LaborLawConstants.TerminationReasonCodes.EmployeeUnilateralC9);
        term.TerminationParty.Should().Be(TerminationParty.Employee);
        term.WrittenNoticeFileId.Should().Be(fileId);
        term.DecidedByUserId.Should().Be(actor.Id);
        term.LegalBasis.Should().Be("Điều 35 k.1");
    }

    /// <summary>
    /// Reason C1 (Hết hạn HĐ) yêu cầu WrittenNotice — KHÔNG upload notice → fail với code
    /// WRITTEN_NOTICE_REQUIRED.
    /// </summary>
    [Fact]
    public async Task Create_Rejects_When_Reason_Requires_Written_Notice_But_FileId_Missing()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var termRepo = new ContractTerminationRepository(fx.Db);
        var storage = new FakeFileStorageService();

        var contractId = await ActivateContractAsync(fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var createTermHandler = new CreateContractTerminationCommandHandler(
            termRepo, repo, lookup, currentUser.Object);

        var act = () => createTermHandler.Handle(new CreateContractTerminationCommand(
            ContractId: contractId,
            ReasonRefId: 1,
            TerminationParty: TerminationParty.AutoExpiry,
            TerminationDate: DateTime.UtcNow.AddDays(30),
            NoticeSentAt: DateTime.UtcNow,
            NoticeDaysAhead: null,
            NoNoticeRequired: false,
            WrittenNoticeFileId: null,
            LegalBasis: "Điều 34 k.1",
            Notes: null), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*WRITTEN_NOTICE_REQUIRED*");

        var count = await fx.Db.ContractTerminations.CountAsync(t => t.ContractId == contractId);
        count.Should().Be(0);
    }

    /// <summary>
    /// Reason C6 (NLĐ chết) RequiresWrittenNotice=false — không cần upload notice → pass.
    /// </summary>
    [Fact]
    public async Task Create_Pass_When_Reason_Has_No_Written_Notice_Requirement()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var termRepo = new ContractTerminationRepository(fx.Db);
        var storage = new FakeFileStorageService();

        var contractId = await ActivateContractAsync(fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var createTermHandler = new CreateContractTerminationCommandHandler(
            termRepo, repo, lookup, currentUser.Object);

        var terminationId = await createTermHandler.Handle(new CreateContractTerminationCommand(
            ContractId: contractId,
            ReasonRefId: 6,
            TerminationParty: TerminationParty.ByLaw,
            TerminationDate: DateTime.UtcNow,
            NoticeSentAt: DateTime.UtcNow,
            NoticeDaysAhead: null,
            NoNoticeRequired: false,
            WrittenNoticeFileId: null,
            LegalBasis: "Điều 34 k.6",
            Notes: "Giấy chứng tử số ..."), CancellationToken.None);

        terminationId.Should().BeGreaterThan(0);

        var contractAfter = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contractId);
        contractAfter.Status.Should().Be(ContractStatus.Terminated);
    }

    /// <summary>
    /// 1-1 constraint — terminate HĐ đã terminate → fail.
    /// </summary>
    [Fact]
    public async Task Create_Rejects_Second_Termination_For_Same_Contract()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var termRepo = new ContractTerminationRepository(fx.Db);
        var storage = new FakeFileStorageService();

        var contractId = await ActivateContractAsync(fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var createTermHandler = new CreateContractTerminationCommandHandler(
            termRepo, repo, lookup, currentUser.Object);

        await createTermHandler.Handle(new CreateContractTerminationCommand(
            ContractId: contractId,
            ReasonRefId: 6,
            TerminationParty: TerminationParty.ByLaw,
            TerminationDate: DateTime.UtcNow,
            NoticeSentAt: DateTime.UtcNow,
            NoticeDaysAhead: null,
            NoNoticeRequired: false,
            WrittenNoticeFileId: null,
            LegalBasis: "Điều 34 k.6",
            Notes: null), CancellationToken.None);

        var act = () => createTermHandler.Handle(new CreateContractTerminationCommand(
            ContractId: contractId,
            ReasonRefId: 6,
            TerminationParty: TerminationParty.ByLaw,
            TerminationDate: DateTime.UtcNow,
            NoticeSentAt: DateTime.UtcNow,
            NoticeDaysAhead: null,
            NoNoticeRequired: false,
            WrittenNoticeFileId: null,
            LegalBasis: "Điều 34 k.6",
            Notes: null), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*expected Active*");
    }

    /// <summary>
    /// Notice rule fail: HĐ vô thời hạn + NoticeDaysAhead = 10 → reject với code
    /// TERMINATION_NOTICE_INDEFINITE_TOO_SHORT.
    /// </summary>
    [Fact]
    public async Task Create_Rejects_When_Notice_Days_Below_Threshold()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var termRepo = new ContractTerminationRepository(fx.Db);
        var storage = new FakeFileStorageService();

        var createCmd = ContractCommandFactory.ValidCreate(
            category: ContractCategory.Indefinite,
            subtype: ContractSubtype.Official,
            employeeInformationId: employeeInfo.Id,
            employerPartyId: party.Id,
            employerSignerUserId: actor.Id,
            positionId: position.Id,
            salaryGradeId: grade.Id) with
        { EffectiveTo = null };

        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser.Object, new ContractNoGenerator(fx.Db));
        var contractId = await createHandler.Handle(createCmd, CancellationToken.None);

        var activateHandler = new ActivateContractCommandHandler(
            repo, lookup, storage, currentUser.Object,
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

        var noticeHandler = new UploadTerminationNoticeCommandHandler(
            repo, storage, currentUser.Object,
            NullLogger<UploadTerminationNoticeCommandHandler>.Instance);
        var noticePdf = BuildFakePdfBytes();
        var fileId = await noticeHandler.Handle(new UploadTerminationNoticeCommand(
            contractId, new MemoryStream(noticePdf), noticePdf.Length, "n.pdf", "application/pdf", null),
            CancellationToken.None);

        var createTermHandler = new CreateContractTerminationCommandHandler(
            termRepo, repo, lookup, currentUser.Object);

        var act = () => createTermHandler.Handle(new CreateContractTerminationCommand(
            ContractId: contractId,
            ReasonRefId: 9,
            TerminationParty: TerminationParty.Employee,
            TerminationDate: DateTime.UtcNow.AddDays(10),
            NoticeSentAt: DateTime.UtcNow,
            NoticeDaysAhead: 10,
            NoNoticeRequired: false,
            WrittenNoticeFileId: fileId,
            LegalBasis: "Điều 35 k.1",
            Notes: null), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*TERMINATION_NOTICE_INDEFINITE_TOO_SHORT*");

        var contractAfter = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contractId);
        contractAfter.Status.Should().Be(ContractStatus.Active);
    }

    /// <summary>
    /// Upload notice 2 lần → file cũ IsCurrent=false, file mới IsCurrent=true, version bump.
    /// </summary>
    [Fact]
    public async Task Upload_Notice_Twice_Bumps_Version_And_Demotes_Old()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var storage = new FakeFileStorageService();

        var contractId = await ActivateContractAsync(fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var noticeHandler = new UploadTerminationNoticeCommandHandler(
            repo, storage, currentUser.Object,
            NullLogger<UploadTerminationNoticeCommandHandler>.Instance);

        var v1Pdf = BuildFakePdfBytes("v1");
        var v1Id = await noticeHandler.Handle(new UploadTerminationNoticeCommand(
            contractId, new MemoryStream(v1Pdf), v1Pdf.Length, "v1.pdf", "application/pdf", null),
            CancellationToken.None);

        var v2Pdf = BuildFakePdfBytes("v2");
        var v2Id = await noticeHandler.Handle(new UploadTerminationNoticeCommand(
            contractId, new MemoryStream(v2Pdf), v2Pdf.Length, "v2.pdf", "application/pdf", null),
            CancellationToken.None);

        v2Id.Should().NotBe(v1Id);

        var files = await fx.Db.ContractFiles.AsNoTracking()
            .Where(f => f.TargetType == FileTargetType.Termination && f.TargetId == contractId)
            .OrderBy(f => f.Version)
            .ToListAsync();

        files.Should().HaveCount(2);
        files[0].Version.Should().Be(1);
        files[0].IsCurrent.Should().BeFalse();
        files[1].Version.Should().Be(2);
        files[1].IsCurrent.Should().BeTrue();
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
