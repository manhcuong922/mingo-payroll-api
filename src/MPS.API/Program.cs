using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MPS.API.Authorization;
using MPS.API.Middleware;
using MPS.API.RateLimiting;
using MPS.API.Serialization;
using MPS.API.Services;
using MPS.Application.Behaviors;
using MPS.Application.Common.Interfaces;
using MPS.Infrastructure.DependencyInjection;
using MPS.Infrastructure.Persistence;
using MPS.Infrastructure.Security;
using MPS.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// ===== API Layer =====
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        // DateTime từ EF/SQL Server bị mất Kind (Unspecified) — ép output có 'Z' để FE parse
        // đúng UTC thay vì local time. Xem UtcDateTimeConverter.
        opts.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        opts.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MPS API",
        Version = "v1",
        Description = "MPS Clean Architecture API"
    });

    var bearerScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: Bearer {access_token}",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT",
        Reference = new Microsoft.OpenApi.Models.OpenApiReference
        {
            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme,
        }
    };
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, bearerScheme);
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [bearerScheme] = Array.Empty<string>()
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditContextProvider, HttpAuditContextProvider>();

// ===== Application Layer =====
var applicationAssembly = typeof(MPS.Application.Features.PerformanceMetric.Commands.PerformanceMetricCreateCommand).Assembly;

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(applicationAssembly);
    // Archive guard chạy TRƯỚC ValidationBehavior để fail-fast khi HĐ archived,
    // tránh waste validation cost trên request chắc chắn bị 409.
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ArchivedContractGuardBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(applicationAssembly);
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(applicationAssembly));

// ===== Infrastructure Layer =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options
        .UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
        .AddInterceptors(sp.GetRequiredService<MPS.Infrastructure.Persistence.Interceptors.AuditSaveChangesInterceptor>()));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddCheck<MinioHealthCheck>("minio", tags: new[] { "storage", "ready" });

// ===== Authentication =====
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
var jwtSettings = jwtSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey)
    || Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32)
{
    throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes (256 bits).");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromSeconds(jwtSettings.ClockSkewSeconds),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });

builder.Services.Configure<MPS.Application.Settings.AttendanceCheckinSettings>(
    builder.Configuration.GetSection(MPS.Application.Settings.AttendanceCheckinSettings.SectionName));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ServiceAccount", policy =>
        policy
            .AddAuthenticationSchemes(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireRole("AiService"));
});
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();
// FT-39 — wrap middleware result handler để ghi audit AccessDenied trước khi trả 403.
builder.Services.AddSingleton<
    Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
    AuditAccessDeniedHandler>();

// ===== Rate Limiting ===== (khai báo chi tiết: MPS.API/RateLimiting/RateLimitingSetup.cs)
builder.Services.AddMpsRateLimiting(builder.Configuration);

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              // Không expose thì trình duyệt giấu header này khỏi JS, mọi endpoint tải file
              // (bảng lương, mẫu phân ca...) mất tên file do server đặt và phải đoán lại ở FE.
              .WithExposedHeaders("Content-Disposition");
              
        }
    });
});

var app = builder.Build();

// ===== Apply migrations + seed default admin =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Seed:ResetOnStartup=true -> XÓA SẠCH database rồi tạo lại từ migration + seed, để DB luôn về đúng
    // trạng thái seeder mỗi lần khởi động (dùng cho môi trường DEMO: mỗi lần deploy = data sạch giống seed).
    // Mặc định false (an toàn cho local/dev/test). Bật qua env Seed__ResetOnStartup ở compose/.env.
    // ⚠️ CẢNH BÁO: bật cờ này thì MỌI lần container api khởi động (deploy, reboot, crash) đều mất toàn bộ dữ liệu.
    if (builder.Configuration.GetValue<bool>("Seed:ResetOnStartup"))
    {
        app.Logger.LogWarning(
            "Seed:ResetOnStartup=true -> xóa toàn bộ database rồi seed lại từ đầu (mọi dữ liệu hiện có sẽ mất).");
        await db.Database.EnsureDeletedAsync();
    }

    // Tự áp các migration còn thiếu khi khởi động (idempotent).
    // Cần thiết cho deploy container: DB mới sẽ được tạo schema đầy đủ.
    await db.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

// ===== Middleware Pipeline =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MPS API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
// Thứ tự quan trọng: UseRateLimiter phải chạy SAU UseAuthentication thì policy phân vùng theo
// người dùng (tải file hợp đồng, OCR) mới đọc được claim; nếu không, mọi người dùng chung một IP
// bị gộp vào một rổ và chặn nhầm nhau. Vẫn đặt TRƯỚC UseAuthorization để request bị từ chối
// quyền cũng bị tính hạn mức (chống dò endpoint).
app.UseAuthentication();
app.UseMiddleware<LoginIdentityMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health/minio", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Name == "minio",
});

app.Run();
