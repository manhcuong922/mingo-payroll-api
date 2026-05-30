using MPS.Domain.Constants;
using MPS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MPS.Infrastructure.Persistence.Configurations;

public class RoleEntityConfiguration : IEntityTypeConfiguration<Role>
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(255);

        builder.HasIndex(e => e.Name).IsUnique();

        builder.HasData(BuildSystemRoles());
    }

    private static IEnumerable<Role> BuildSystemRoles() => new[]
    {
        NewSystemRole(SystemRoles.SystemAdminId, SystemRoles.SystemAdmin, "Quản trị hệ thống — toàn quyền."),
        NewSystemRole(SystemRoles.HrId, SystemRoles.Hr, "Phòng Nhân sự — quản lý nhân viên & thông tin nhân sự."),
        NewSystemRole(SystemRoles.AccountantId, SystemRoles.Accountant, "Kế toán — đọc thông tin lương/thuế nhân viên."),
        NewSystemRole(SystemRoles.DirectorId, SystemRoles.Director, "Giám đốc — duyệt KPI, quản lý phòng ban."),
        NewSystemRole(SystemRoles.DepartmentManagerId, SystemRoles.DepartmentManager, "Trưởng phòng — quản lý KPI nhân viên trong phòng."),
        NewSystemRole(SystemRoles.ProjectManagerId, SystemRoles.ProjectManager, "Quản lý dự án — theo dõi KPI dự án."),
        NewSystemRole(SystemRoles.EmployeeId, SystemRoles.Employee, "Nhân viên — chỉ xem/sửa thông tin của chính mình."),
    };

    private static Role NewSystemRole(int id, string name, string description) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        IsSystem = true,
        CreatedAt = SeedTimestamp,
        UpdatedAt = null,
    };
}
