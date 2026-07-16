using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MPS.Domain.Entities.Attendance;

namespace MPS.Infrastructure.Persistence.Configurations.Attendance;

public class FaceRegistrationEntityConfiguration : IEntityTypeConfiguration<FaceRegistration>
{
    public void Configure(EntityTypeBuilder<FaceRegistration> builder)
    {
        builder.ToTable("FaceRegistrations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.EmployeeInformationId).IsRequired();
        builder.Property(e => e.PhotoObjectKey).IsRequired().HasMaxLength(500);
        builder.Property(e => e.EmbeddingJson).IsRequired();
        builder.Property(e => e.Status).HasConversion<byte>().IsRequired();
        builder.Property(e => e.RejectReason).HasMaxLength(1000);
        builder.Property(e => e.SubmittedAt).IsRequired();
        builder.Property(e => e.ReviewedAt);

        builder.HasIndex(e => e.EmployeeInformationId);
        builder.HasIndex(e => e.Status);

        builder.HasOne(e => e.EmployeeInformation)
            .WithMany()
            .HasForeignKey(e => e.EmployeeInformationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ReviewedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
