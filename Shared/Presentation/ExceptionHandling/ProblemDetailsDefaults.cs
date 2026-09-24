using Microsoft.AspNetCore.Mvc;

namespace OrderCore.Api.Shared.Presentation.ExceptionHandling;

/// <summary>
/// Gives every <see cref="ProblemDetails"/> the API writes a <c>code</c>,
/// not only the ones produced by <see cref="ApiExceptionHandler"/>: the
/// framework's own responses (model-validation 400, unknown route 404,
/// the authorization middleware's 401/403, written by <c>UseStatusCodePages</c>)
/// would otherwise arrive without one, and clients branch on <c>code</c>.
/// A code that is already set is never overwritten.
/// </summary>
public static class ProblemDetailsDefaults
{
    public const string UnauthenticatedCode = "unauthenticated";
    public const string ForbiddenCode = "forbidden";
    public const string NotFoundCode = "not_found";

    public static void AddDefaultCode(ProblemDetailsContext context)
    {
        var extensions = context.ProblemDetails.Extensions;
        if (extensions.ContainsKey(ApiExceptionHandler.ErrorCodeExtension))
        {
            return;
        }

        var code = DefaultCodeFor(context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode);
        if (code is not null)
        {
            extensions[ApiExceptionHandler.ErrorCodeExtension] = code;
        }
    }

    public static string? DefaultCodeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ApiExceptionHandler.ValidationErrorCode,
        StatusCodes.Status401Unauthorized => UnauthenticatedCode,
        StatusCodes.Status403Forbidden => ForbiddenCode,
        StatusCodes.Status404NotFound => NotFoundCode,
        >= 500 => ApiExceptionHandler.InternalErrorCode,
        _ => null,
    };
}
