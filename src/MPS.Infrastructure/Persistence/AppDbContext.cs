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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
