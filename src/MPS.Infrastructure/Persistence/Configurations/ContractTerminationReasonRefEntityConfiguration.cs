using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Constants;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ContractTerminationReasonRefEntityConfiguration : IEntityTypeConfiguration<ContractTerminationReasonRef>
{
    public void Configure(EntityTypeBuilder<ContractTerminationReasonRef> builder)
    {
        builder.ToTable("ContractTerminationReasonRefs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique();

        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.RequiresWrittenNotice).IsRequired();

        builder.HasData(BuildSeed());
    }

    // Seed 13 lý do chấm dứt HĐLĐ theo Điều 34 BLLĐ 2019.
    // RequiresWrittenNotice = false cho khoản 4–8 (Điều 45).
    private static IEnumerable<ContractTerminationReasonRef> BuildSeed() => new[]
    {
        R(1,  LaborLawConstants.TerminationReasonCodes.ExpiryC1,               "Hết hạn HĐLĐ (trừ k.4 Điều 177).", true),
        R(2,  LaborLawConstants.TerminationReasonCodes.WorkCompletedC2,        "Đã hoàn thành công việc theo HĐLĐ.", true),
        R(3,  LaborLawConstants.TerminationReasonCodes.MutualAgreementC3,      "Hai bên thỏa thuận chấm dứt HĐLĐ.", true),
        R(4,  LaborLawConstants.TerminationReasonCodes.CriminalSentenceC4,     "NLĐ bị kết án tù không hưởng án treo / tử hình / cấm làm công việc.", false),
        R(5,  LaborLawConstants.TerminationReasonCodes.ForeignerDeportedC5,    "NLĐ là người nước ngoài bị trục xuất.", false),
        R(6,  LaborLawConstants.TerminationReasonCodes.EmployeeDeathC6,        "NLĐ chết / Tòa tuyên mất năng lực hành vi / mất tích / đã chết.", false),
        R(7,  LaborLawConstants.TerminationReasonCodes.EmployerDissolvedC7,    "NSDLĐ cá nhân chết / mất năng lực hành vi; tổ chức chấm dứt hoạt động.", false),
        R(8,  LaborLawConstants.TerminationReasonCodes.DisciplinaryDismissalC8,"NLĐ bị xử lý kỷ luật sa thải.", false),
        R(9,  LaborLawConstants.TerminationReasonCodes.EmployeeUnilateralC9,   "NLĐ đơn phương chấm dứt HĐLĐ (Điều 35).", true),
        R(10, LaborLawConstants.TerminationReasonCodes.EmployerUnilateralC10,  "NSDLĐ đơn phương chấm dứt HĐLĐ (Điều 36).", true),
        R(11, LaborLawConstants.TerminationReasonCodes.EmployerLayoffC11,      "NSDLĐ cho NLĐ thôi việc (Điều 42, 43).", true),
        R(12, LaborLawConstants.TerminationReasonCodes.WorkPermitExpiredC12,   "Giấy phép lao động hết hiệu lực (Điều 156).", true),
        R(13, LaborLawConstants.TerminationReasonCodes.ProbationFailedC13,     "Thỏa thuận thử việc không đạt / một bên hủy thỏa thuận thử việc.", true),
    };

    private static ContractTerminationReasonRef R(int id, string code, string description, bool requiresWrittenNotice) => new()
    {
        Id = id,
        Code = code,
        Description = description,
        RequiresWrittenNotice = requiresWrittenNotice,
    };
}
