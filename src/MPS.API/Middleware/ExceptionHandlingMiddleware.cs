using System.Net;
using System.Text.Json;
using FluentValidation;
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
}
