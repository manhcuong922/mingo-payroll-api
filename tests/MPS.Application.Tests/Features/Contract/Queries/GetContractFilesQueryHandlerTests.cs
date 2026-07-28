using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.Contract.Queries;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Services;

namespace MPS.Application.Tests.Features.Contract.Queries;

/// <summary>
/// Integration test cho 3 query files (Contract/Termination/Appendix) — Phase 10+ task
/// (FE blocker §14.1). Verify:
/// - Trả đúng files theo TargetType+TargetId, sort Version DESC
/// - Self-scope: caller chỉ có .Self phải khớp EmployeeId
/// - 404 khi contract/appendix không tồn tại
/// - Presigned URL được gen cho mỗi file
/// </summary>
public sealed class GetContractFilesQueryHandlerTests
{
    /// <summary>
    /// Happy path: HR có Read.All → list tất cả file của contract. Verify sort Version DESC
    /// và mỗi file có PresignedDownloadUrl khác rỗng.
    /// </summary>
    [Fact]
    public async Task Returns_Contract_Files_Sorted_DESC_With_Presigned_Url()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);
        currentUser.Setup(c => c.HasPermission(Permissions.ContractFileReadAll)).Returns(true);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var storage = new FakeFileStorageService();

        // Tạo HĐ → activate v1 → replace để có v2 IsCurrent=true + v1 IsCurrent=false.
        var contractId = await ActivateAndReplaceAsync(fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var handler = new GetContractFilesQueryHandler(repo, storage, currentUser.Object);
        var result = await handler.Handle(new GetContractFilesQuery(contractId), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Version.Should().Be(2, "sort Version DESC → v2 trước");
        result[0].IsCurrent.Should().BeTrue();
        result[1].Version.Should().Be(1);
        result[1].IsCurrent.Should().BeFalse();
        result.Should().AllSatisfy(f =>
        {
            f.PresignedDownloadUrl.Should().StartWith("https://fake.minio/");
            f.TargetType.Should().Be("Contract");
            f.TargetId.Should().Be(contractId);
        });
    }

    /// <summary>
    /// Self-scope: caller chỉ có Read.Self và contract.EmployeeId khác → throw Forbidden.
    /// </summary>
    [Fact]
    public async Task Throws_Forbidden_When_Self_Scope_And_Contract_Belongs_To_Other_Employee()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        // HR tạo + activate contract cho employeeInfo.
        var hrUser = new Mock<ICurrentUserService>();
        hrUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var storage = new FakeFileStorageService();

        var contractId = await ActivateContractAsync(fx, repo, lookup, preflight, storage, hrUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        // "Stranger" cố đọc → chỉ có Read.Self, UserId khác EmployeeId.
        var stranger = new Mock<ICurrentUserService>();
        stranger.SetupGet(c => c.UserId).Returns(99999);
        stranger.Setup(c => c.HasPermission(Permissions.ContractFileReadSelf)).Returns(true);

        var handler = new GetContractFilesQueryHandler(repo, storage, stranger.Object);
        var act = () => handler.Handle(new GetContractFilesQuery(contractId), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.ForbiddenAccessException>()
            .WithMessage("*only read files of their own*");
    }

    /// <summary>Contract không tồn tại → throw DomainValidationException (mapped 400 ở middleware).</summary>
    [Fact]
    public async Task Throws_When_Contract_Not_Found()
    {
        using var fx = new ContractIntegrationFixture();
        await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(c => c.HasPermission(Permissions.ContractFileReadAll)).Returns(true);

        var repo = new ContractRepository(fx.Db);
        var storage = new FakeFileStorageService();

        var handler = new GetContractFilesQueryHandler(repo, storage, currentUser.Object);
        var act = () => handler.Handle(new GetContractFilesQuery(99999), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*Contract 99999 not found*");
    }

    /// <summary>Contract tồn tại nhưng chưa upload file nào → return empty list, không throw.</summary>
    [Fact]
    public async Task Returns_Empty_When_Contract_Has_No_Files()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);
        currentUser.Setup(c => c.HasPermission(Permissions.ContractFileReadAll)).Returns(true);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);

        // Tạo draft contract, KHÔNG activate (no file uploaded).
        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser.Object, new ContractNoGenerator(fx.Db));
        var contractId = await createHandler.Handle(
            ContractCommandFactory.ValidCreate(
                employeeInformationId: employeeInfo.Id, employerPartyId: party.Id,
                employerSignerUserId: actor.Id, positionId: position.Id, salaryGradeId: grade.Id),
            CancellationToken.None);

        var handler = new GetContractFilesQueryHandler(repo, new FakeFileStorageService(), currentUser.Object);
        var result = await handler.Handle(new GetContractFilesQuery(contractId), CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ===== Helpers =====

    private static async Task<int> ActivateContractAsync(
        ContractIntegrationFixture fx, ContractRepository repo, ContractLookupReader lookup,
        ContractPreflightChecker preflight, FakeFileStorageService storage, ICurrentUserService currentUser,
        int actorId, int employeeInformationId, int partyId, int positionId, int gradeId)
    {
        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser, new ContractNoGenerator(fx.Db));
        var contractId = await createHandler.Handle(
            ContractCommandFactory.ValidCreate(
                employeeInformationId: employeeInformationId, employerPartyId: partyId,
                employerSignerUserId: actorId, positionId: positionId, salaryGradeId: gradeId),
            CancellationToken.None);

        var activateHandler = new ActivateContractCommandHandler(
            repo, lookup, storage, currentUser,
            Mock.Of<IEmployeeInformationRepository>(),
            Mock.Of<IEmployeeAccountProvisioner>(),
            Mock.Of<IEmailSender>(),
            new NoopPayrollSourceChangeHandler(),
            new NoopLeaveBalanceInitializer(),
            NullLogger<ActivateContractCommandHandler>.Instance);
        var pdf = BuildFakePdfBytes();
        await activateHandler.Handle(new ActivateContractCommand(
            ContractId: contractId, PdfStream: new MemoryStream(pdf), FileSizeBytes: pdf.Length,
            FileName: "v1.pdf", ContentType: "application/pdf",
            SignedAt: DateTime.UtcNow, SignedPlace: "HN", SignedBySource: null),
            CancellationToken.None);
        return contractId;
    }

    private static async Task<int> ActivateAndReplaceAsync(
        ContractIntegrationFixture fx, ContractRepository repo, ContractLookupReader lookup,
        ContractPreflightChecker preflight, FakeFileStorageService storage, ICurrentUserService currentUser,
        int actorId, int employeeInformationId, int partyId, int positionId, int gradeId)
    {
        var contractId = await ActivateContractAsync(fx, repo, lookup, preflight, storage, currentUser,
            actorId, employeeInformationId, partyId, positionId, gradeId);

        var replaceHandler = new ReplaceContractFileCommandHandler(
            repo, storage, currentUser,
            NullLogger<ReplaceContractFileCommandHandler>.Instance);
        var pdf = BuildFakePdfBytes("v2-content");
        await replaceHandler.Handle(new ReplaceContractFileCommand(
            ContractId: contractId, PdfStream: new MemoryStream(pdf), FileSizeBytes: pdf.Length,
            FileName: "v2.pdf", ContentType: "application/pdf", SignedBySource: "mingo"),
            CancellationToken.None);
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
