using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// DbContext chính - cấu hình kết nối SQL Server và ánh xạ Entity sang bảng database.
/// </summary>
public class ExampleDbContext : DbContext
{
    public ExampleDbContext(DbContextOptions<ExampleDbContext> options) : base(options) { }

    public DbSet<ExampleEntity> Examples => Set<ExampleEntity>();
    public DbSet<PerformanceMetric> PerformanceMetrics => Set<PerformanceMetric>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<TypePerformanceMetric> TypePerformanceMetrics => Set<TypePerformanceMetric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExampleDbContext).Assembly);
    }
}
