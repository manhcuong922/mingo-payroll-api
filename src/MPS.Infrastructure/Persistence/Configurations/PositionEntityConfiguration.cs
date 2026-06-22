using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace MPS.Infrastructure.Persistence.Configurations
{
    public class PositionEntityConfiguration : IEntityTypeConfiguration<Position>
    {
        public void Configure(EntityTypeBuilder<Position> builder)
        {
            builder.ToTable("Positions");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Code)
                .HasMaxLength(20)
                .IsRequired();

            builder.HasIndex(e => e.Code).IsUnique();

            builder.Property(e => e.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(e => e.Description)
                .HasColumnType("nvarchar(max)");
        }
    }
}
