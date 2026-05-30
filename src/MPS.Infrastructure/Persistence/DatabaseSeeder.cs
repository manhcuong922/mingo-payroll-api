using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Constants;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Persistence;

public sealed class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedDefaultAdminAsync(cancellationToken);
    }

    private async Task SeedDefaultAdminAsync(CancellationToken cancellationToken)
    {
        var adminExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.RoleId == SystemRoles.SystemAdminId, cancellationToken);

        if (adminExists)
        {
            _logger.LogDebug("System admin already exists; skipping default admin seed.");
            return;
        }

        var section = _configuration.GetSection("DefaultAdmin");
        var username = section["Username"] ?? "admin";
        var employeeCode = section["EmployeeCode"] ?? "ADMIN001";
        var email = section["Email"] ?? "admin@mingo.local";
        var fullName = section["FullName"] ?? "System Administrator";
        var phone = section["PhoneNo"] ?? "0000000000";
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("DefaultAdmin:Password is not configured; skipping default admin seed.");
            return;
        }

        var admin = new User
        {
            EmployeeCode = employeeCode,
            FullName = fullName,
            RoleId = SystemRoles.SystemAdminId,
            Username = username,
            PasswordHash = _passwordHasher.Hash(password),
            Email = email,
            Dob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNo = phone,
            Status = 1,
            PasswordChangedAt = DateTime.UtcNow,
        };

        await _context.Users.AddAsync(admin, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded default system admin user '{Username}'.", username);
    }
}
