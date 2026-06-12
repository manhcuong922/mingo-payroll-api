using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Behaviors;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Services;

namespace MPS.Application.Tests.BackgroundJobs;

/// <summary>
/// Integration test cho archive guard end-to-end qua MediatR pipeline:
/// HĐ archived → bất kỳ command nào implement IContractScopedCommand đều bị chặn,
/// trả về <see cref="ContractArchivedException"/> trước khi handler chạy.
/// </summary>
public sealed class ArchiveGuardIntegrationTests
{
    /// <summary>
    /// CancelContractCommand trên HĐ archived → throw ContractArchivedException, KHÔNG
    /// hit handler (vì behavior fail-fast trước).
    /// </summary>
    [Fact]
    public async Task Cancel_On_Archived_Contract_Throws_ContractArchivedException_Via_Pipeline()
    {
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);

        var createHandler = new CreateContractCommandHandler(repo, preflight, currentUser.Object, new ContractNoGenerator(fx.Db));
        var contractId = await createHandler.Handle(
            ContractCommandFactory.ValidCreate(
                employeeInformationId: employeeInfo.Id,
                employerPartyId: party.Id,
                employerSignerUserId: actor.Id,
                positionId: position.Id,
                salaryGradeId: grade.Id),
            CancellationToken.None);

        // Archive tay (mô phỏng output của ContractArchiveJob).
        var contract = await fx.Db.Contracts.FirstAsync(c => c.Id == contractId);
        contract.ArchivedAt = DateTime.UtcNow.AddYears(-11);
        await fx.Db.SaveChangesAsync();

        // Wire MediatR + behavior + handler.
        var services = new ServiceCollection();
        services.AddSingleton<IContractRepository>(repo);
        services.AddSingleton<ICurrentUserService>(currentUser.Object);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CancelContractCommand).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ArchivedContractGuardBehavior<,>));
        });
        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var act = () => mediator.Send(new CancelContractCommand(contractId));

        var ex = await act.Should().ThrowAsync<ContractArchivedException>();
        ex.Which.ContractId.Should().Be(contractId);

        // Verify Status KHÔNG đổi (handler không chạy).
        var saved = await fx.Db.Contracts.AsNoTracking().FirstAsync(c => c.Id == contractId);
        saved.Status.Should().Be(ContractStatus.Draft,
            "guard phải fail-fast trước handler — Status vẫn Draft");
    }
}
