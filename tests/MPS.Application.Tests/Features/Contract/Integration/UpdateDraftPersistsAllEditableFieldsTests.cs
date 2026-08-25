using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Contract.Commands;
using MPS.Application.Features.Contract.Common;
using MPS.Application.Features.Contract.Queries;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Repositories;
using MPS.Infrastructure.Services;

namespace MPS.Application.Tests.Features.Contract.Integration;

/// <summary>
/// Sửa HĐ nháp phải LƯU ĐƯỢC mọi trường mà lệnh sửa nhận vào.
///
/// <para><b>Bối cảnh.</b> <c>UpdateContractCommandHandler</c> đọc HĐ bằng <c>GetByIdAsync</c>
/// (<c>AsNoTracking</c>) rồi gọi <c>ContractBuilder.ApplyUpdate</c> lên bản DETACHED đó. Bản detached
/// KHÔNG bao giờ được lưu — <c>ContractRepository.UpdateDraftAsync</c> nạp lại entity tracked và chép
/// sang bằng một danh sách trường VIẾT TAY (<c>ApplyScalarFields</c>). Trường nào quên trong danh
/// sách đó thì thay đổi bị nuốt IM LẶNG: API vẫn trả 200 "đã cập nhật", DB giữ nguyên giá trị cũ.</para>
///
/// <para>Ba trường từng bị quên đúng kiểu này: <c>Kind</c>, <c>TaxPolicy</c>,
/// <c>UnionFundExemptFlag</c>. Hai cái sau chảy thẳng vào bảng lương —
/// <c>PayrollDataCollector.ResolveTaxFlag</c> / <c>ResolveInsuranceFlag</c> — nên sai là sai TIỀN.</para>
/// </summary>
public sealed class UpdateDraftPersistsAllEditableFieldsTests
{
    [Fact]
    public async Task UpdateDraft_Persists_UnionFundExemptFlag_And_TaxPolicy_And_Kind()
    {
        // ===== Arrange — tạo HĐ nháp với KPCĐ mặc định (DN vẫn trích) =====
        using var fx = new ContractIntegrationFixture();
        var (actor, employeeInfo, party, position, grade, _) = await fx.SeedBaselineAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(actor.Id);
        currentUser.Setup(c => c.HasPermission(MPS.Domain.Constants.Permissions.ContractReadAll)).Returns(true);

        var repo = new ContractRepository(fx.Db);
        var lookup = new ContractLookupReader(fx.Db);
        var preflight = new ContractPreflightChecker(lookup, repo);

        var createHandler = new CreateContractCommandHandler(
            repo, preflight, currentUser.Object, new ContractNoGenerator(fx.Db));

        // Service = kind UserChoice ⇒ HR tự khai cờ BH + chính sách thuế.
        var createCmd = ContractCommandFactory.ValidCreate(
            kind: ContractKind.Service,
            category: null,
            subtype: null,
            employeeInformationId: employeeInfo.Id,
            employerPartyId: party.Id,
            employerSignerUserId: actor.Id,
            positionId: position.Id,
            salaryGradeId: grade.Id);

        var contractId = await createHandler.Handle(createCmd, CancellationToken.None);

        // ===== Act — HR sửa lại: miễn KPCĐ + đổi chính sách thuế sang luỹ tiến =====
        var updateHandler = new UpdateContractCommandHandler(
            repo, preflight, Mock.Of<IPayrollStaleMarker>(),
            NullLogger<UpdateContractCommandHandler>.Instance);

        var updated = await updateHandler.Handle(
            new UpdateContractCommand(
                Id: contractId,
                Kind: ContractKind.Collaborator,               // ⭐ đổi loại HĐ (cùng khung dân sự)
                ContractCategory: createCmd.ContractCategory,
                ContractSubtype: createCmd.ContractSubtype,
                EmployeeInformationId: createCmd.EmployeeInformationId,
                EmployerPartyId: createCmd.EmployerPartyId,
                EmployerSignerUserId: createCmd.EmployerSignerUserId,
                PositionId: createCmd.PositionId,
                JobDescription: createCmd.JobDescription,
                WorkLocation: createCmd.WorkLocation,
                EffectiveFrom: createCmd.EffectiveFrom,
                EffectiveTo: createCmd.EffectiveTo,
                SalaryGradeId: createCmd.SalaryGradeId,
                SalaryPayCycle: createCmd.SalaryPayCycle,
                PromotionPolicy: createCmd.PromotionPolicy,
                WorkingTimeJson: createCmd.WorkingTimeJson,
                PpePolicy: createCmd.PpePolicy,
                SocialInsuranceFlag: createCmd.SocialInsuranceFlag,
                HealthInsuranceFlag: createCmd.HealthInsuranceFlag,
                UnemploymentInsuranceFlag: createCmd.UnemploymentInsuranceFlag,
                UnionFundExemptFlag: true,                       // ⭐ HR tick "Miễn trích KPCĐ"
                TaxPolicy: TaxPolicy.ProgressivePit,                // ⭐ đổi cách tính thuế
                TrainingPolicy: createCmd.TrainingPolicy,
                ConfidentialityClause: createCmd.ConfidentialityClause,
                TemplateId: createCmd.TemplateId,
                HasProbation: createCmd.HasProbation,
                ProbationStartDate: createCmd.ProbationStartDate,
                ProbationEndDate: createCmd.ProbationEndDate,
                ProbationSalary: createCmd.ProbationSalary,
                ProbationResultStatus: createCmd.ProbationResultStatus,
                ParentContractId: createCmd.ParentContractId,
                Allowances: Array.Empty<ContractAllowanceInput>()),
            CancellationToken.None);

        // ===== Assert — đọc lại đúng những gì vừa lưu =====
        updated.Should().BeTrue();

        var getByIdHandler = new GetContractByIdQueryHandler(repo, fx.Mapper, currentUser.Object);
        var detail = await getByIdHandler.Handle(new GetContractByIdQuery(contractId), CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.UnionFundExemptFlag.Should().BeTrue(
            "HR đã tick 'Miễn trích KPCĐ' — nuốt mất là kỳ lương vẫn trích 2% KPCĐ của người này");
        detail.TaxPolicy.Should().Be(nameof(TaxPolicy.ProgressivePit),
            "chính sách thuế quyết định khấu trừ 10% hay luỹ tiến — sai là sai tiền thuế");
        detail.Kind.Should().Be(nameof(ContractKind.Collaborator),
            "loại HĐ quyết định khung pháp lý, chế độ BH và cả tiền tố mã HĐ");
    }
}
