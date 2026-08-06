using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.ContractAppendix.Commands;
using MPS.Application.Features.ContractAppendix.Common;
using MPS.Application.Features.ContractAppendix.Queries;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Services;

namespace MPS.Application.Tests.Features.ContractAppendix.Queries;

/// <summary>
/// Test cho query files của appendix — self-scope check phức tạp hơn vì phải resolve
/// appendix → contract → check EmployeeId.
/// </summary>
public sealed class GetAppendixFilesQueryHandlerTests
{
    [Fact]
    public async Task Returns_Appendix_Files_With_Presigned_Url()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);
        currentUser.Setup(c => c.HasPermission(Permissions.ContractFileReadAll)).Returns(true);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);
        var appendixRepo = new ContractAppendixRepository(fx.Db);
        var applier = new AppendixApplier(lookup);
        var storage = new FakeFileStorageService();

        // Setup: tạo HĐ Active → tạo + activate appendix (sẽ tạo ContractFile target=Appendix).
        var contractId = await CreateActiveContractAsync(
            fx, repo, lookup, preflight, storage, currentUser.Object,
            actor.Id, employeeInfo.Id, party.Id, position.Id, grade.Id);

        var createApdx = new CreateContractAppendixCommandHandler(appendixRepo, repo, currentUser.Object);
        var appendixId = (await createApdx.Handle(new CreateContractAppendixCommand(
            ContractId: contractId,
            Title: "Test",
            ChangedClausesJson: "{\"jobDescription\":\"Senior\"}",
            AppendixText: "...",
            EffectiveFrom: DateTime.UtcNow.AddDays(14)), CancellationToken.None)).Id;

        var activateApdx = new ActivateContractAppendixCommandHandler(
            appendixRepo, applier, storage, currentUser.Object,
            NullLogger<ActivateContractAppendixCommandHandler>.Instance,
            new NoopPayrollSourceChangeHandler());
        var pdf = BuildFakePdfBytes();
        await activateApdx.Handle(new ActivateContractAppendixCommand(
            AppendixId: appendixId,
            PdfStream: new MemoryStream(pdf), FileSizeBytes: pdf.Length,
            FileName: "apdx.pdf", ContentType: "application/pdf",
            SignedAt: DateTime.UtcNow, SignedBySource: null), CancellationToken.None);

        // Act
        var handler = new GetAppendixFilesQueryHandler(appendixRepo, repo, storage, currentUser.Object);
        var result = await handler.Handle(new GetAppendixFilesQuery(appendixId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].TargetType.Should().Be("Appendix");
        result[0].TargetId.Should().Be(appendixId);
        result[0].Version.Should().Be(1);
        result[0].IsCurrent.Should().BeTrue();
        result[0].PresignedDownloadUrl.Should().StartWith("https://fake.minio/");
    }

    [Fact]
    public async Task Throws_When_Appendix_Not_Found()
    {
        using var fx = new ContractIntegrationFixture();
        await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(c => c.HasPermission(Permissions.ContractFileReadAll)).Returns(true);

        var repo = new ContractRepository(fx.Db);
        var appendixRepo = new ContractAppendixRepository(fx.Db);

        var handler = new GetAppendixFilesQueryHandler(appendixRepo, repo, new FakeFileStorageService(), currentUser.Object);
        var act = () => handler.Handle(new GetAppendixFilesQuery(99999), CancellationToken.None);

        await act.Should().ThrowAsync<MPS.Domain.Exceptions.DomainValidationException>()
            .WithMessage("*Appendix 99999 not found*");
    }

    private static async Task<int> CreateActiveContractAsync(
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
            Mock.Of<IFirstLoginInvitationService>(),
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
