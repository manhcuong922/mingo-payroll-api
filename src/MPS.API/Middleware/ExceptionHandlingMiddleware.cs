using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Exceptions.Storage;
using MPS.Domain.Exceptions;

namespace MPS.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException vex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            var errors = vex.Errors.Select(e => e.ErrorMessage).ToList();
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { errors }));
        }
        catch (ContractArchivedException caex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = ContractArchivedException.ErrorCode,
                error = caex.Message,
                contractId = caex.ContractId,
            }));
        }
        catch (NotFoundException nfex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = nfex.Message }));
        }
        catch (PayrollNotReadyException prex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "PAYROLL_NOT_READY",
                error = prex.Message,
                notReady = prex.NotReady.Select(r => new
                {
                    r.EmployeeInformationId,
                    r.EmployeeCode,
                    r.FullName,
                    r.Missing,
                }),
            }, JsonOptions));
        }
        catch (ConflictException cfex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = cfex.Message }));
        }
        catch (RegularHoursCapWarningException rcex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "REGULAR_HOURS_CAP_WARNING",
                error = rcex.Message,
                dailyCapWarnings = rcex.DailyWarnings,
                weeklyCapWarnings = rcex.WeeklyWarnings,
            }, JsonOptions));
        }
        catch (RegularHoursCapExceededException rhex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "REGULAR_HOURS_CAP_EXCEEDED",
                error = rhex.Message,
                dailyCapViolations = rhex.DailyViolations,
                weeklyCapViolations = rhex.WeeklyViolations,
            }, JsonOptions));
        }
        catch (WeeklyRegularHoursCapWarningException wcex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "WEEKLY_REGULAR_HOURS_CAP_WARNING",
                error = wcex.Message,
                weeklyCapWarnings = wcex.Warnings,
            }, JsonOptions));
        }
        catch (DomainValidationException dvex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { errors = new[] { dvex.Message } }));
        }
        catch (ForbiddenAccessException fex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = fex.Message }));
        }
        catch (SystemRoleModificationException srex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = srex.Message }));
        }
        catch (RoleInUseException riex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = riex.Message }));
        }
        catch (InvalidObjectKeyException iokex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { errors = new[] { iokex.Message } }));
        }
        catch (FileNotFoundInStorageException fnfex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = fnfex.Message }));
        }
        catch (FileStorageUnavailableException fsuex)
        {
            _logger.LogWarning(fsuex,
                "File storage unavailable for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Storage is temporarily unavailable." }));
        }
        catch (FileStorageException fsex)
        {
            _logger.LogError(fsex,
                "File storage error for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "A storage error occurred." }));
        }
        catch (DbUpdateException dbex) when (IsUniqueViolation(dbex))
        {
            // Race check-then-insert (#15/#17): sequential thì validator trả 400 sạch, nhưng 2 request
            // song song cùng lọt qua validator rồi đụng unique index ⇒ trước đây trả 500, client retry
            // vô nghĩa. Map về 409 để client biết "ai đó vừa tạo trước".
            _logger.LogWarning(dbex,
                "Unique constraint violated for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "DUPLICATE_KEY",
                error = "Dữ liệu đã tồn tại (trùng khoá duy nhất) — có thể một yêu cầu khác vừa tạo trước.",
            }));
        }
        catch (DbUpdateException dbex) when (IsForeignKeyViolation(dbex))
        {
            // #19/#26: xoá bản ghi còn dữ liệu tham chiếu (đơn đã Completed, hồ sơ NV còn HĐ Active…).
            // FK constraint là lỗi NGHIỆP VỤ của người gọi, không phải sự cố hệ thống ⇒ 409, không 500.
            _logger.LogWarning(dbex,
                "Foreign key constraint violated for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "REFERENCED_BY_OTHER_RECORDS",
                error = "Không thể thực hiện: bản ghi đang được dữ liệu khác tham chiếu.",
            }));
        }
        catch (DbUpdateException dbex)
        {
            _logger.LogError(dbex,
                "Database update failed for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "A database error occurred." }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "An unexpected error occurred." }));
        }
    }

    // SQL Server: 2601 = duplicate key row in unique index, 2627 = unique/PK constraint violation.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    // 547 = REFERENCE constraint (FK) — cả chiều insert lẫn chiều delete-restrict.
    private static bool IsForeignKeyViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 547 };
}
