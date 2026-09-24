using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OrderCore.Api.Shared.Presentation.OpenApi;

/// <summary>
/// Makes authentication visible in the generated OpenAPI document: declares
/// the <c>Bearer</c> (JWT) scheme once, and marks every operation that
/// isn't <c>[AllowAnonymous]</c> as requiring it, with its 401 and 403
/// responses. That way Scalar can send the token, and each action doesn't
/// have to repeat two <c>[ProducesResponseType]</c> attributes that are
/// the same everywhere.
/// </summary>
public sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token from POST /api/auth/sign-in (or sign-up / refresh).",
        };

        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
        });

        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Not signed in, or the access token is invalid or expired." });

        if (metadata.OfType<IAuthorizeData>().Any(a => a.Policy is not null))
        {
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Signed in, but not allowed (wrong role)." });
        }

        return Task.CompletedTask;
    }
}
