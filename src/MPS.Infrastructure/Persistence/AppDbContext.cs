using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PerformanceMetric> PerformanceMetrics => Set<PerformanceMetric>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<TypePerformanceMetric> TypePerformanceMetrics => Set<TypePerformanceMetric>();
    public DbSet<User> Users => Set<User>();
    public DbSet<EmployeeInformation> EmployeeInformations => Set<EmployeeInformation>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Contract module
    public DbSet<ContractParty> ContractParties => Set<ContractParty>();
    public DbSet<ContractTemplate> ContractTemplates => Set<ContractTemplate>();
    public DbSet<ContractTerminationReasonRef> ContractTerminationReasonRefs => Set<ContractTerminationReasonRef>();
    public DbSet<SalaryGrade> SalaryGrades => Set<SalaryGrade>();
    public DbSet<AllowanceType> AllowanceTypes => Set<AllowanceType>();
    public DbSet<ContractFile> ContractFiles => Set<ContractFile>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractAllowance> ContractAllowances => Set<ContractAllowance>();
    public DbSet<ContractAppendix> ContractAppendices => Set<ContractAppendix>();
    public DbSet<ContractTermination> ContractTerminations => Set<ContractTermination>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
