using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Shared.Presentation.ExceptionHandling;

/// <summary>
/// The project's single exception-handling convention (instead of a
/// try/catch per endpoint): translates the exceptions use cases and
/// aggregates throw into an RFC 7807 <see cref="ProblemDetails"/> response
/// with the matching status and a stable <c>code</c> extension the client
/// can branch on. Registered in <c>Program.cs</c> through
/// <c>AddExceptionHandler</c> + <c>UseExceptionHandler</c>.
///
/// <see cref="ArgumentException"/> is mapped as a validation error because
/// that is what every <c>Create</c>/value-object factory already throws for
/// invalid input. Anything not listed here is an unexpected failure (a
/// bug), so it becomes a 500 whose body never carries exception details
/// outside Development.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    public const string ErrorCodeExtension = "code";
    public const string ValidationErrorCode = "validation_error";
    public const string ConcurrencyConflictCode = "concurrency_conflict";
    public const string InternalErrorCode = "internal_error";

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetailsService, IHostEnvironment environment, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = Classify(exception);

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError && !_environment.IsDevelopment() ? null : exception.Message,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions[ErrorCodeExtension] = code;

        httpContext.Response.StatusCode = status;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    public static (int Status, string Code, string Title) Classify(Exception exception) => exception switch
    {
        DomainRuleViolationException e => (StatusCodes.Status400BadRequest, e.Code, "Business rule violated."),
        NotFoundException e => (StatusCodes.Status404NotFound, e.Code, "Resource not found."),
        ConflictException e => (StatusCodes.Status409Conflict, e.Code, "Request conflicts with the current state."),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ConcurrencyConflictCode, "Request conflicts with the current state."),
        ArgumentException => (StatusCodes.Status400BadRequest, ValidationErrorCode, "Invalid request."),
        _ => (StatusCodes.Status500InternalServerError, InternalErrorCode, "An unexpected error occurred."),
    };
}
