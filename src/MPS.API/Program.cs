using MPS.Infrastructure.DependencyInjection;
using MPS.Infrastructure.Persistence;
using MPS.API.Middleware;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ===== API Layer =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MPS API",
        Version = "v1",
        Description = "MPS Clean Architecture API"
    });
});

// ===== Application Layer =====
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(MPS.Application.Features.Example.Commands.ExampleCreateCommand).Assembly));

builder.Services.AddAutoMapper(typeof(MPS.Application.Mappings.ExampleMappingProfile).Assembly);

// ===== Infrastructure Layer =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<ExampleDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsAssembly(typeof(ExampleDbContext).Assembly.FullName)));

builder.Services.AddInfrastructure();

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ===== Middleware Pipeline =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MPS API v1");
        c.RoutePrefix = string.Empty; // Swagger UI at root
    });
}

app.UseMiddleware<ExampleExceptionMiddleware>();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
