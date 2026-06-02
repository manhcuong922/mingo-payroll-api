using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence.Configurations;

public class ContractEntityConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.ContractNo).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.ContractNo).IsUnique();

        builder.Property(e => e.ContractCategory).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ContractSubtype).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Form).HasMaxLength(20).IsRequired();
        builder.Property(e => e.JobDescription).IsRequired();
        builder.Property(e => e.WorkLocation).HasMaxLength(500).IsRequired();
        builder.Property(e => e.EffectiveFrom).IsRequired();
        builder.Property(e => e.EffectiveTo);

        builder.Property(e => e.SalaryAmountSnapshot).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.SalaryCurrencySnapshot).HasMaxLength(3).IsRequired();
        builder.Property(e => e.SalaryFormType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.SalaryPayCycle).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PromotionPolicy);
        builder.Property(e => e.WorkingTimeJson).IsRequired();
        builder.Property(e => e.PpePolicy).HasMaxLength(1000);
        builder.Property(e => e.SocialInsuranceFlag).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.HealthInsuranceFlag).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.UnemploymentInsuranceFlag).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.TrainingPolicy);
        builder.Property(e => e.ConfidentialityClause);
        builder.Property(e => e.ContractText).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ConvertedFromDefiniteFlag).IsRequired().HasDefaultValue(false);

        builder.Property(e => e.HasProbation).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.ProbationStartDate);
        builder.Property(e => e.ProbationEndDate);
        builder.Property(e => e.ProbationSalary).HasColumnType("decimal(18,2)");
        builder.Property(e => e.ProbationResultStatus).HasMaxLength(20);
        builder.Property(e => e.ProbationResultNotifiedAt);
        builder.Property(e => e.ProbationCancelledByParty).HasMaxLength(10);

        builder.Property(e => e.SignedAt);
        builder.Property(e => e.SignedPlace).HasMaxLength(200);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.ArchivedAt);

        builder.HasIndex(e => new { e.EmployeeId, e.Status });
        builder.HasIndex(e => new { e.EffectiveFrom, e.EffectiveTo });

        builder.HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EmployerParty)
            .WithMany(p => p.Contracts)
            .HasForeignKey(e => e.EmployerPartyId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EmployerSignerUser)
            .WithMany()
            .HasForeignKey(e => e.EmployerSignerUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Position)
            .WithMany()
            .HasForeignKey(e => e.PositionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SalaryGrade)
            .WithMany(s => s.Contracts)
            .HasForeignKey(e => e.SalaryGradeId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Template)
            .WithMany(t => t.Contracts)
            .HasForeignKey(e => e.TemplateId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ParentContract)
            .WithMany(c => c.ChildContracts)
            .HasForeignKey(e => e.ParentContractId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
