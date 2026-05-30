using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MPS.Infrastructure.Persistence.Configurations;

public class RefreshTokenEntityConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(e => e.RevokedReason).HasMaxLength(255);
        builder.Property(e => e.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(e => e.CreatedByIp).HasMaxLength(64);
        builder.Property(e => e.RevokedByIp).HasMaxLength(64);

        builder.Ignore(e => e.IsActive);

        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.ExpiresAt });

        builder.HasOne(e => e.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
